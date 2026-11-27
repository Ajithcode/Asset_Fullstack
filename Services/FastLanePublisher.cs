using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using StackExchange.Redis;

namespace NetworkServer.Services;

/// <summary>
/// Fast lane: every parsed reading goes to the Redis stream {FastLane:Stream} (default networkserver_fast) as soon as the
/// MQTT message arrives, with its own scan time, so the worker can decide a tag's zone in seconds instead of per 15 s window.
/// Stateless and additive: TagAggregator, the Mongo time series and networkserver_stream are not touched.
/// One stream entry per gateway message: gw (gateway id), recv (server receive ms), src, r = JSON array of readings
///   [tagId, rssi, tsMs, kind, deviceId, frameHash]
/// kind "g" = tag heard by the gateway itself (deviceId = gateway), "r" = tag relayed by a repeater (deviceId = repeater;
/// frameHash identifies the relay frame so the worker can drop frames the repeater simply re-advertises).
/// tsMs is the tag entry's own scan time, or the receive time when the gateway sent none. Only tags in
/// {Aggregation:AllowedTags} are published (empty list = all). The stream is trimmed to about {FastLane:MaxLength}.
/// Publishing runs on a background channel, so a slow Redis never blocks the MQTT broker.
/// </summary>
public class FastLanePublisher : BackgroundService
{
    private record Entry(string Gateway, long RecvMs, string Source, string Readings);

    private readonly Channel<Entry> _channel = Channel.CreateBounded<Entry>(
        new BoundedChannelOptions(5000) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<FastLanePublisher> _logger;
    private readonly bool _enabled;
    private readonly RedisKey _stream;
    private readonly int _maxLength;
    private readonly HashSet<string> _allowedTags;
    private readonly HashSet<string> _repeaterTags;
    private long _dropped;

    public FastLanePublisher(IConnectionMultiplexer redis, IConfiguration config, ILogger<FastLanePublisher> logger)
    {
        _redis = redis;
        _logger = logger;
        _enabled = config.GetValue("FastLane:Enabled", true);
        _stream = config["FastLane:Stream"] ?? "networkserver_fast";
        _maxLength = config.GetValue("FastLane:MaxLength", 20_000);
        _allowedTags = Normalize(config.GetSection("Aggregation:AllowedTags").Get<string[]>());
        _repeaterTags = Normalize(config.GetSection("Aggregation:RepeaterTags").Get<string[]>());
    }

    private static HashSet<string> Normalize(string[]? ids) =>
        (ids ?? []).Select(t => t.Replace(":", "").Trim().ToLowerInvariant()).ToHashSet();

    /// <summary>Converts the batch to readings right away (the batch's tag objects are reused by TagAggregator) and queues it.</summary>
    public void Enqueue(string source, GatewayBatch batch)
    {
        if (!_enabled)
            return;
        var recvMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var readings = new JsonArray();
        foreach (var tag in batch.Tags)
        {
            var tagId = GatewayPayload.TagId(tag);
            var ts = TimeMs(tag) ?? recvMs;
            if (!_repeaterTags.Contains(tagId))
            {
                if (_allowedTags.Count == 0 || _allowedTags.Contains(tagId))
                    AddReading(readings, tagId, tag, ts, "g", batch.GatewayId, "");
                continue;
            }

            var frame = FrameHash((string?)tag["raw"]);
            foreach (var relayed in RepeaterPayload.RelayedTags(tag))
            {
                var relayedId = ResolveTagId(GatewayPayload.TagId(relayed));
                if (_allowedTags.Count == 0 || _allowedTags.Contains(relayedId))
                    AddReading(readings, relayedId, relayed, ts, "r", tagId, frame);
            }
        }
        if (readings.Count == 0)
            return;
        if (!_channel.Writer.TryWrite(new Entry(batch.GatewayId, recvMs, source, readings.ToJsonString())))
            Interlocked.Increment(ref _dropped);
    }

    private static void AddReading(JsonArray readings, string tagId, JsonObject tag, long ts, string kind, string device, string frame)
    {
        var rssi = GatewayPayload.Rssi(tag); // handles parsed (any number type) and relayed (int) values
        if (rssi == double.MinValue)
            return; // nothing to locate with
        readings.Add(new JsonArray(tagId, rssi, ts, kind, device, frame));
    }

    private static long? TimeMs(JsonObject tag) =>
        tag["time"] is JsonValue v && v.TryGetValue<string>(out var s)
        && DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out var t)
            ? t.ToUnixTimeMilliseconds() : null;

    private static string FrameHash(string? raw) =>
        raw is null ? "" : Convert.ToHexString(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(raw)))[..10].ToLowerInvariant();

    // Relay frames usually carry only the last bytes of a tag's mac ("6206"); complete it from the allowed tags
    // when exactly one of them ends with it (same rule as TagAggregator).
    private string ResolveTagId(string mac)
    {
        if (mac.Length == 12)
            return mac;
        var matches = _allowedTags.Where(t => t.EndsWith(mac) && !_repeaterTags.Contains(t)).Take(2).ToList();
        return matches.Count == 1 ? matches[0] : mac;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("Fast lane is off (FastLane:Enabled=false)");
            return;
        }
        _logger.LogInformation("Fast lane on: stream {Stream}, max length {Max}", (string?)_stream, _maxLength);
        var published = 0L;
        var lastLog = DateTime.UtcNow;
        try
        {
            await foreach (var e in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await _redis.GetDatabase().StreamAddAsync(_stream,
                        [new("gw", e.Gateway), new("recv", e.RecvMs), new("src", e.Source), new("r", e.Readings)],
                        maxLength: _maxLength, useApproximateMaxLength: true);
                    published++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Interlocked.Increment(ref _dropped);
                    _logger.LogWarning(ex, "Fast lane publish failed");
                }
                if (DateTime.UtcNow - lastLog > TimeSpan.FromMinutes(5))
                {
                    _logger.LogInformation("Fast lane: {Published} entries published, {Dropped} dropped", published, Interlocked.Read(ref _dropped));
                    lastLog = DateTime.UtcNow;
                }
            }
        }
        catch (OperationCanceledException) { }
    }
}
