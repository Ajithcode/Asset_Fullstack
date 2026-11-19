using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace A360.Tracking.Api.Locating;

/// <summary>
/// One front-end WebSocket connection (ws://host:5490/ws/locate?access_token=<JWT>[&tagid=...], on the Tracking & IoT service).
/// The sign-in token is required: without a valid one the connection is refused with 401.
///
/// Front end -> service (JSON text frames):
///   {"type":"subscribe","tagid":"c3000099138f"}   look up the tag's latest gateway and start receiving its responses
///   "c3000099138f"  or  c3000099138f            same as subscribe
///   {"type":"action","payload":{...}}            publish payload to /gw/{gateway}/action of the current gateway
///   {"type":"unsubscribe"}                       stop receiving responses
///   {"type":"led","tagid":"c30000000002","color":3,"onMs":200,"offMs":200,"cycles":25,"brightness":100}
///                                                blink the tag's LED (options may also be nested: "led":{...})
///   {"type":"buzzer","tagid":"c30000000002","beepMs":300,"pauseMs":300,"cycles":5,"level":0}
///                                                beep the tag's buzzer (or nested: "buzzer":{...})
///   {"type":"both","tagid":"c30000000002","led":{"color":3,"onMs":200,"offMs":200,"cycles":25,"brightness":100},
///    "buzzer":{"beepMs":200,"pauseMs":200,"cycles":25,"level":0}}
///                                                LED and buzzer in one gateway message (type "locate" works too)
///   Every command goes to the gateway as {"version":2,"req_id":n,"cmds":[{"action":42,...LED},{"action":56,...buzzer}]}.
///     tagid is optional once subscribed. Missing options use the defaults (LED 3/300/300/10/100, buzzer 300/300/5/0).
///
/// Service -> front end:
///   {"type":"subscribed","tagid","gatewayid","lastSeen","topics":{"action","response"}}
///   {"type":"response","tagid","gatewayid","topic","receivedAt","payload"}
///   {"type":"actionSent","gatewayid","topic"}
///   {"type":"unsubscribed"}
///   {"type":"commandSent","reqId","command" (led | buzzer | both),"tagid","gatewayid","topic","options","sentAt"}
///   {"type":"commandStatus","reqId","command","tagid","gatewayid","status","code","error","message"}
///     status: accepted (gateway code 1) -> completed (code 2, error 0: tag is blinking / beeping)
///             | failed (code 2, error != 0) | timeout (no code 2 within Locating:CommandTimeoutSeconds)
///   {"type":"error","message"}
/// </summary>
public class LocatingSession(
    WebSocket socket,
    TagGatewayLookup lookup,
    GatewayMqttBridge bridge,
    LocateCommandStore commands,
    IOptions<LocatingSettings> options,
    ILogger<LocatingSession> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // Outgoing frames go through a queue so a slow browser never blocks the shared MQTT receive loop.
    private readonly Channel<JsonObject> _outbox = Channel.CreateBounded<JsonObject>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    private sealed record PendingCommand(LocateCommand Command, string GatewayId, CancellationTokenSource Timeout);

    // req_id -> command sent from this session and still waiting for its final answer.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, PendingCommand> _pending = new();

    private TagGateway? _current;
    private IAsyncDisposable? _subscription;

    public async Task RunAsync(string? initialTagId, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var sender = SendLoopAsync(cts.Token);
        try
        {
            if (!string.IsNullOrWhiteSpace(initialTagId))
                await SubscribeAsync(initialTagId, cts.Token);
            await ReceiveLoopAsync(cts.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException)
        {
        }
        finally
        {
            if (_subscription != null)
                await _subscription.DisposeAsync();
            foreach (var pending in _pending.Values)
                pending.Timeout.Cancel();
            _outbox.Writer.TryComplete();
            cts.Cancel();
            try { await sender; } catch { /* socket already gone */ }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();

        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", ct);
                return;
            }

            message.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage)
                continue;

            var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            message.SetLength(0);
            if (result.MessageType == WebSocketMessageType.Text)
                await HandleAsync(text, ct);
        }
    }

    private async Task HandleAsync(string text, CancellationToken ct)
    {
        JsonNode? node;
        try { node = JsonNode.Parse(text); }
        catch (JsonException) { node = JsonValue.Create(text.Trim()); } // bare tag id

        try
        {
            switch (node)
            {
                case JsonValue v when v.TryGetValue<string>(out var tagId):
                    await SubscribeAsync(tagId, ct);
                    break;

                case JsonObject obj:
                    var type = obj["type"]?.GetValue<string>()?.ToLowerInvariant();
                    switch (type)
                    {
                        case "subscribe" or null when obj["tagid"] is not null:
                            await SubscribeAsync(obj["tagid"]!.GetValue<string>(), ct);
                            break;
                        case "action":
                            await SendActionAsync(obj["payload"], ct);
                            break;
                        case var command when LocateCommandKind.FromType(command) is { } kind:
                            await SendCommandAsync(kind, obj, ct);
                            break;
                        case "unsubscribe":
                            await UnsubscribeAsync();
                            Send(new JsonObject { ["type"] = "unsubscribed" });
                            break;
                        default:
                            SendError($"Unknown message type '{type}'.");
                            break;
                    }
                    break;

                default:
                    SendError("Send a tag id or a JSON object with a \"type\".");
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to handle message {Message}", text);
            SendError(ex.Message);
        }
    }

    private async Task SubscribeAsync(string rawTagId, CancellationToken ct)
    {
        var tagId = TagGatewayLookup.NormalizeId(rawTagId);
        if (tagId.Length == 0)
        {
            SendError("tagid is empty.");
            return;
        }

        var found = await lookup.FindLatestAsync(tagId, ct);
        if (found is null)
        {
            SendError($"No NetworkTimeSeries data with a gatewayid for tag '{tagId}'.");
            return;
        }

        // Switching tags: drop the old gateway's responses first (no-op if it's the same gateway and tag).
        if (_current is { } previous && previous.GatewayId == found.GatewayId && previous.TagId == found.TagId && _subscription != null)
        {
            _current = found;
        }
        else
        {
            await UnsubscribeAsync();
            _current = found;
            _subscription = await bridge.SubscribeAsync(found.GatewayId, found.TagId, OnResponse, ct);
        }

        var settings = options.Value;
        Send(new JsonObject
        {
            ["type"] = "subscribed",
            ["tagid"] = found.TagId,
            ["gatewayid"] = found.GatewayId,
            ["lastSeen"] = found.LastSeen,
            ["rssi"] = found.Rssi,
            ["mqttConnected"] = bridge.IsConnected,
            ["topics"] = new JsonObject
            {
                ["action"] = settings.ActionTopicFor(found.GatewayId),
                ["response"] = settings.ResponseTopicFor(found.GatewayId),
            },
        });
        logger.LogInformation("Session watching tag {TagId} via gateway {GatewayId}", found.TagId, found.GatewayId);
    }

    private async Task UnsubscribeAsync()
    {
        if (_subscription != null)
            await _subscription.DisposeAsync();
        _subscription = null;
        _current = null;
    }

    private async Task SendActionAsync(JsonNode? payload, CancellationToken ct)
    {
        if (_current is not { } current)
        {
            SendError("Subscribe to a tag before sending an action.");
            return;
        }

        // A string payload is published as-is, anything else as JSON.
        var text = payload is JsonValue v && v.TryGetValue<string>(out var s) ? s : payload?.ToJsonString() ?? "";
        var topic = await bridge.PublishActionAsync(current.GatewayId, text, ct);
        Send(new JsonObject { ["type"] = "actionSent", ["gatewayid"] = current.GatewayId, ["topic"] = topic });
    }

    private async Task SendCommandAsync(string kind, JsonObject message, CancellationToken ct)
    {
        // A tag id on the command switches to that tag (and its latest gateway) first.
        var requested = message["tagid"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(requested) && TagGatewayLookup.NormalizeId(requested) != _current?.TagId)
            await SubscribeAsync(requested, ct);

        if (_current is not { } current)
        {
            SendError("Send a tagid with the command (or subscribe to a tag first).");
            return;
        }

        LocateCommand command;
        try { command = LocateCommandBuilder.Build(kind, current.TagId, message, options.Value); }
        catch (ArgumentException ex)
        {
            SendError(ex.Message);
            return;
        }

        var sentAt = DateTime.UtcNow;
        var timeout = new CancellationTokenSource();
        _pending[command.ReqId] = new PendingCommand(command, current.GatewayId, timeout);

        string topic;
        try
        {
            topic = await bridge.PublishActionAsync(current.GatewayId, command.Message.ToJsonString(), ct);
        }
        catch
        {
            _pending.TryRemove(command.ReqId, out _);
            throw;
        }
        await commands.InsertAsync(command, current.GatewayId, topic, sentAt, ct);

        Send(new JsonObject
        {
            ["type"] = "commandSent",
            ["reqId"] = command.ReqId,
            ["command"] = command.Kind,
            ["tagid"] = command.TagId,
            ["gatewayid"] = current.GatewayId,
            ["topic"] = topic,
            ["options"] = command.Options.DeepClone(),
            ["sentAt"] = sentAt,
        });
        logger.LogInformation("Sent {Command} req_id {ReqId} for tag {TagId} via gateway {GatewayId}",
            command.Kind, command.ReqId, command.TagId, current.GatewayId);

        _ = WatchTimeoutAsync(command.ReqId, timeout.Token);
    }

    private async Task WatchTimeoutAsync(int reqId, CancellationToken token)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(options.Value.CommandTimeoutSeconds), token); }
        catch (OperationCanceledException) { return; }

        if (!_pending.TryRemove(reqId, out var pending))
            return;
        await commands.MarkTimeoutAsync(pending.GatewayId, reqId);
        SendStatus(pending, GatewayReply.StatusTimeout, null, null);
    }

    // Turns a gateway reply to one of this session's commands into a commandStatus message.
    private void TrackReply(GatewayResponse response)
    {
        if (GatewayReply.TryParse(response.Payload) is not { Status: { } status } reply
            || !_pending.TryGetValue(reply.ReqId, out var pending)
            || pending.GatewayId != response.GatewayId)
            return;

        if (status != GatewayReply.StatusAccepted && _pending.TryRemove(reply.ReqId, out _))
            pending.Timeout.Cancel();
        SendStatus(pending, status, reply.Code, reply.Error);
    }

    private void SendStatus(PendingCommand pending, string status, int? code, int? error)
    {
        var what = pending.Command.Kind switch
        {
            LocateCommandKind.Led => "blinking",
            LocateCommandKind.Buzzer => "beeping",
            _ => "blinking and beeping",
        };
        Send(new JsonObject
        {
            ["type"] = "commandStatus",
            ["reqId"] = pending.Command.ReqId,
            ["command"] = pending.Command.Kind,
            ["tagid"] = pending.Command.TagId,
            ["gatewayid"] = pending.GatewayId,
            ["status"] = status,
            ["code"] = code,
            ["error"] = error,
            ["message"] = status switch
            {
                GatewayReply.StatusAccepted => "Gateway accepted the command.",
                GatewayReply.StatusCompleted => $"Tag is {what}.",
                GatewayReply.StatusFailed => $"Tag did not respond (error {error}).",
                _ => "No answer from the gateway in time.",
            },
        });
    }

    private ValueTask OnResponse(GatewayResponse response)
    {
        TrackReply(response);

        JsonNode? payload;
        try { payload = JsonNode.Parse(response.Payload); }
        catch (JsonException) { payload = response.Payload; }

        Send(new JsonObject
        {
            ["type"] = "response",
            ["tagid"] = _current?.TagId,
            ["gatewayid"] = response.GatewayId,
            ["topic"] = response.Topic,
            ["receivedAt"] = response.ReceivedAt,
            ["payload"] = payload,
        });
        return ValueTask.CompletedTask;
    }

    private void SendError(string message) => Send(new JsonObject { ["type"] = "error", ["message"] = message });

    private void Send(JsonObject message) => _outbox.Writer.TryWrite(message);

    private async Task SendLoopAsync(CancellationToken ct)
    {
        await foreach (var message in _outbox.Reader.ReadAllAsync(ct))
        {
            if (socket.State != WebSocketState.Open)
                return;
            var bytes = Encoding.UTF8.GetBytes(message.ToJsonString(JsonOptions));
            await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
        }
    }
}
