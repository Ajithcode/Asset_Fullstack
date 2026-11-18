using System.Text;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace A360.Tracking.Api.Locating;

/// <summary>A message received on a gateway's response topic.</summary>
public record GatewayResponse(string GatewayId, string Topic, DateTime ReceivedAt, string Payload);

/// <summary>
/// One shared MQTT connection for every WebSocket session.
/// <see cref="SubscribeAsync"/> subscribes to /gw/{gateway}/response while at least one session watches that gateway
/// (subscriptions are reference counted and restored after a reconnect). Each received response is stored once in
/// {Mongo:ResponseCollection} and then handed to every session watching that gateway.
/// <see cref="PublishActionAsync"/> publishes to /gw/{gateway}/action.
/// </summary>
public class GatewayMqttBridge : BackgroundService
{
    private sealed class Watcher(string tagId, Func<GatewayResponse, ValueTask> onResponse)
    {
        public string TagId { get; } = tagId;
        public Func<GatewayResponse, ValueTask> OnResponse { get; } = onResponse;
    }

    private readonly LocatingSettings _settings;
    private readonly IMongoCollection<BsonDocument> _responses;
    private readonly LocateCommandStore _commands;
    private readonly ILogger<GatewayMqttBridge> _logger;
    private readonly IMqttClient _client = new MqttFactory().CreateMqttClient();
    // Serializes MQTT subscribe/unsubscribe calls; _watchers itself is guarded by lock (_watchers).
    private readonly SemaphoreSlim _subscriptionLock = new(1, 1);
    private readonly Dictionary<string, List<Watcher>> _watchers = new(); // response topic -> watchers

    public GatewayMqttBridge(IOptions<LocatingSettings> options, IMongoDatabase database,
        LocateCommandStore commands, ILogger<GatewayMqttBridge> logger)
    {
        _commands = commands;
        _settings = options.Value;
        _responses = database.GetCollection<BsonDocument>(options.Value.ResponseCollection);
        _logger = logger;
        _client.ApplicationMessageReceivedAsync += OnMessageAsync;
    }

    public bool IsConnected => _client.IsConnected;

    /// <summary>Starts forwarding the gateway's responses to <paramref name="onResponse"/>; dispose the result to stop.</summary>
    public async Task<IAsyncDisposable> SubscribeAsync(string gatewayId, string tagId,
        Func<GatewayResponse, ValueTask> onResponse, CancellationToken ct)
    {
        var topic = _settings.ResponseTopicFor(gatewayId);
        var watcher = new Watcher(tagId, onResponse);

        await _subscriptionLock.WaitAsync(ct);
        try
        {
            bool isNew;
            lock (_watchers)
            {
                isNew = !_watchers.TryGetValue(topic, out var list);
                if (isNew)
                    _watchers[topic] = list = [];
                list!.Add(watcher);
            }
            // Not connected: the topic is subscribed by the reconnect loop.
            if (isNew && _client.IsConnected)
                await SubscribeTopicsAsync([topic], ct);
        }
        finally
        {
            _subscriptionLock.Release();
        }

        return new Subscription(() => UnsubscribeAsync(topic, watcher));
    }

    public async Task<string> PublishActionAsync(string gatewayId, string payload, CancellationToken ct)
    {
        if (!_client.IsConnected)
            throw new InvalidOperationException("MQTT broker is not connected.");

        var topic = _settings.ActionTopicFor(gatewayId);
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)_settings.Qos)
            .Build();
        await _client.PublishAsync(message, ct);
        _logger.LogInformation("Published action to {Topic}: {Payload}", topic, Preview(payload));
        return topic;
    }

    private async Task UnsubscribeAsync(string topic, Watcher watcher)
    {
        await _subscriptionLock.WaitAsync();
        try
        {
            lock (_watchers)
            {
                if (!_watchers.TryGetValue(topic, out var list) || !list.Remove(watcher) || list.Count > 0)
                    return;
                _watchers.Remove(topic);
            }
            if (_client.IsConnected)
            {
                await _client.UnsubscribeAsync(topic);
                _logger.LogInformation("Unsubscribed from {Topic}", topic);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to unsubscribe from {Topic}", topic);
        }
        finally
        {
            _subscriptionLock.Release();
        }
    }

    private async Task SubscribeTopicsAsync(IReadOnlyCollection<string> topics, CancellationToken ct)
    {
        if (topics.Count == 0)
            return;
        var options = new MqttClientSubscribeOptionsBuilder();
        foreach (var topic in topics)
            options.WithTopicFilter(topic, (MqttQualityOfServiceLevel)_settings.Qos);
        await _client.SubscribeAsync(options.Build(), ct);
        _logger.LogInformation("Subscribed to {Topics}", string.Join(", ", topics));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureIndexesAsync(stoppingToken);
        await _commands.EnsureIndexesAsync(stoppingToken);

        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(_settings.Host, _settings.Port)
            .WithClientId(_settings.ClientId)
            .WithCleanSession(true)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(_settings.KeepAliveSeconds));
        if (!string.IsNullOrEmpty(_settings.Username))
            builder.WithCredentials(_settings.Username, _settings.Password);
        if (_settings.UseTls)
            builder.WithTlsOptions(o => o.UseTls());
        var clientOptions = builder.Build();

        var disconnected = new TaskCompletionSource();
        _client.DisconnectedAsync += e =>
        {
            if (!stoppingToken.IsCancellationRequested)
                _logger.LogWarning(e.Exception, "MQTT disconnected from {Host}:{Port} ({Reason})",
                    _settings.Host, _settings.Port, e.Reason);
            disconnected.TrySetResult();
            return Task.CompletedTask;
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                disconnected = new TaskCompletionSource();
                await _client.ConnectAsync(clientOptions, stoppingToken);
                _logger.LogInformation("MQTT connected to {Host}:{Port}", _settings.Host, _settings.Port);

                // Clean session: restore the subscriptions of the sessions that are still watching.
                await _subscriptionLock.WaitAsync(stoppingToken);
                List<string> topics;
                lock (_watchers) topics = [.. _watchers.Keys];
                try { await SubscribeTopicsAsync(topics, stoppingToken); }
                finally { _subscriptionLock.Release(); }

                await disconnected.Task.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT connection to {Host}:{Port} failed", _settings.Host, _settings.Port);
                if (_client.IsConnected)
                    await _client.DisconnectAsync();
            }

            try { await Task.Delay(TimeSpan.FromSeconds(_settings.ReconnectDelaySeconds), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        if (_client.IsConnected)
            await _client.DisconnectAsync();
    }

    private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

        List<Watcher> watchers;
        lock (_watchers)
        {
            if (!_watchers.TryGetValue(topic, out var list))
                return; // late message after the last session left
            watchers = [.. list];
        }

        var gatewayId = GatewayIdFromTopic(topic);
        var response = new GatewayResponse(gatewayId, topic, DateTime.UtcNow, payload);
        _logger.LogInformation("Response on {Topic} ({Bytes} bytes): {Preview}", topic, payload.Length, Preview(payload));

        try
        {
            await _responses.InsertOneAsync(ToDocument(response, watchers.Select(w => w.TagId).Distinct()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store response from {Topic} in {Collection}", topic, _settings.ResponseCollection);
        }

        // LED / buzzer outcome: matched to the stored command by req_id (once, not per session).
        if (GatewayReply.TryParse(payload) is { } reply)
            await _commands.ApplyResponseAsync(gatewayId, reply, payload, response.ReceivedAt);

        foreach (var watcher in watchers)
        {
            try { await watcher.OnResponse(response); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to forward response from {Topic} to a session", topic); }
        }
    }

    private static BsonDocument ToDocument(GatewayResponse response, IEnumerable<string> tagIds) => new()
    {
        { "_id", ObjectId.GenerateNewId() },
        { "receivedAt", response.ReceivedAt },
        { "gatewayid", response.GatewayId },
        { "tagids", new BsonArray(tagIds) },
        { "topic", response.Topic },
        { "payload", ParsePayload(response.Payload) },
        { "raw", response.Payload },
    };

    // JSON payloads are stored as documents/arrays so they can be queried; anything else as a string.
    private static BsonValue ParsePayload(string payload)
    {
        var trimmed = payload.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            try { return BsonSerializer.Deserialize<BsonValue>(payload); }
            catch (FormatException) { }
        }
        return payload;
    }

    // Uses the configured template to pull the gateway id back out of the topic.
    private string GatewayIdFromTopic(string topic)
    {
        var template = _settings.ResponseTopic;
        var index = template.IndexOf("{gateway}", StringComparison.Ordinal);
        if (index < 0)
            return "";
        var suffix = template[(index + "{gateway}".Length)..];
        return topic.Length >= index + suffix.Length && topic.EndsWith(suffix, StringComparison.Ordinal)
            ? topic[index..^suffix.Length]
            : "";
    }

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        try
        {
            await _responses.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("gatewayid").Descending("receivedAt")),
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("tagids").Descending("receivedAt")),
            ], ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not create indexes on {Collection}", _settings.ResponseCollection);
        }
    }

    public override void Dispose()
    {
        _client.Dispose();
        base.Dispose();
    }

    private static string Preview(string s) => s.Length > 200 ? s[..200] + "..." : s;

    private sealed class Subscription(Func<Task> unsubscribe) : IAsyncDisposable
    {
        private int _disposed;
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                await unsubscribe();
        }
    }
}
