using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Driver;

namespace NetworkServer.Services;

/// <summary>
/// Keeps gateway messages in memory. Every {Aggregation:WindowSeconds} (default 15) it writes one message per
/// gateway (and per sender/topic/format) holding each tag only once - the one with the closest (strongest) RSSI
/// seen in that window - and then clears the buffer for the next window. Data is stored as
///   [{"gatewayid":"c851fbf72b00","timestamp":"2026-10-08T04:05:39.000Z"},{"tagid":..,"rssi":..,"raw":..,
///     "batterypercentage":..,"device_trigger":..,"status":..}, ...]
/// Only tags listed in {Aggregation:AllowedTags} are kept (an empty or missing list keeps every tag); a gateway
/// with no allowed tags in the window writes nothing. Repeaters ({Aggregation:RepeaterTags}) are written as gateways
/// of their own, holding the tags they relay (see Add).
/// Each message is written to the time-series collection (see TimeSeriesWriter) and added to the Redis stream
/// (see RedisStreamPublisher).
/// </summary>
public class TagAggregator : BackgroundService
{
    private const int MaxUnsent = 10_000;

    private record GroupKey(string Kind, string GatewayId, string Source, string? DeviceId, string? Topic);

    private readonly object _lock = new();
    private Dictionary<GroupKey, Dictionary<string, JsonObject>> _window = new(); // gateway -> tagid -> closest tag
    // Tags only send their battery in some frames, so the last known value is kept across windows.
    private readonly Dictionary<string, int> _lastBattery = new(); // tagid -> battery %
    // Messages whose insert / publish failed, retried next window.
    private readonly List<BsonDocument> _unsentTimeSeries = [];
    private readonly List<DeviceMessage> _unpublished = [];
    private readonly TimeSeriesWriter _timeSeries;
    private readonly RedisStreamPublisher _publisher;
    private readonly TimeSpan _interval;
    private readonly HashSet<string> _allowedTags;
    private readonly HashSet<string> _repeaterTags;
    private readonly ILogger<TagAggregator> _logger;

    public TagAggregator(IConfiguration config, TimeSeriesWriter timeSeries,
        RedisStreamPublisher publisher, ILogger<TagAggregator> logger)
    {
        _timeSeries = timeSeries;
        _publisher = publisher;
        _interval = TimeSpan.FromSeconds(config.GetValue("Aggregation:WindowSeconds", 15));
        _allowedTags = (config.GetSection("Aggregation:AllowedTags").Get<string[]>() ?? [])
            .Select(t => t.Replace(":", "").Trim().ToLowerInvariant())
            .ToHashSet();
        _repeaterTags = (config.GetSection("Aggregation:RepeaterTags").Get<string[]>() ?? [])
            .Select(t => t.Replace(":", "").Trim().ToLowerInvariant())
            .ToHashSet();
        _logger = logger;
    }

    /// <summary>
    /// Buffers the batch's allowed tags and returns how many that was. A repeater ({Aggregation:RepeaterTags}) is not
    /// stored as a tag of the gateway that heard it: it becomes a gateway of its own (gatewayid = repeater tag id)
    /// holding the tags decoded from its relay frames. That group doesn't depend on which gateway heard the
    /// repeater, so each repeater id ends up as exactly one gateway.
    /// </summary>
    public int Add(string source, string? deviceId, string? topic, GatewayBatch batch)
    {
        var count = 0;
        lock (_lock)
        {
            foreach (var tag in batch.Tags)
            {
                var tagId = GatewayPayload.TagId(tag);
                if (!_repeaterTags.Contains(tagId))
                {
                    count += BufferTag(new GroupKey(batch.Kind, batch.GatewayId, source, deviceId, topic), tag);
                    continue;
                }

                var repeaterKey = new GroupKey("repeater", tagId, source, null, null);
                foreach (var relayed in RepeaterPayload.RelayedTags(tag))
                {
                    relayed["tagid"] = ResolveTagId(GatewayPayload.TagId(relayed));
                    count += BufferTag(repeaterKey, relayed);
                }
            }
        }
        return count;
    }

    // Keeps the tag if it is allowed and the closest one seen for its gateway in this window. Caller holds _lock.
    private int BufferTag(GroupKey key, JsonObject tag)
    {
        var tagId = GatewayPayload.TagId(tag);
        if (_allowedTags.Count > 0 && !_allowedTags.Contains(tagId))
            return 0;

        if (!_window.TryGetValue(key, out var bestByTag))
            _window[key] = bestByTag = new();
        if (GatewayPayload.Battery(tag) is { } battery)
            _lastBattery[tagId] = battery;

        if (!bestByTag.TryGetValue(tagId, out var best) || GatewayPayload.Rssi(tag) > GatewayPayload.Rssi(best))
            bestByTag[tagId] = best = tag;
        best["batterypercentage"] = _lastBattery.TryGetValue(tagId, out var last) ? last : null;
        return 1;
    }

    // Relay frames usually carry only the last bytes of a tag's mac ("6206"); complete it from the allowed tags
    // when exactly one of them ends with it, otherwise keep it as is.
    private string ResolveTagId(string mac)
    {
        if (mac.Length == 12)
            return mac;
        var matches = _allowedTags.Where(t => t.EndsWith(mac) && !_repeaterTags.Contains(t)).Take(2).ToList();
        return matches.Count == 1 ? matches[0] : mac;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Writing gateway messages every {Seconds}s", _interval.TotalSeconds);
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await FlushAsync(stoppingToken);
        }
        catch (OperationCanceledException) { }

        await FlushAsync(CancellationToken.None); // don't lose the last partial window on shutdown
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        Dictionary<GroupKey, Dictionary<string, JsonObject>> window;
        lock (_lock)
        {
            window = _window;
            _window = new();
        }

        var receivedAt = DateTime.UtcNow;
        foreach (var (key, bestByTag) in window)
        {
            var data = new JsonArray
            {
                new JsonObject
                {
                    ["gatewayid"] = key.GatewayId,
                    ["timestamp"] = receivedAt.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
                },
            };
            foreach (var tag in bestByTag.Values.OrderByDescending(GatewayPayload.Rssi))
                data.Add(tag);
            var json = data.ToJsonString();

            var message = new DeviceMessage
            {
                Id = ObjectId.GenerateNewId(),
                ReceivedAt = receivedAt,
                Source = key.Source,
                DeviceId = key.DeviceId,
                Topic = key.Topic,
                Format = key.Kind,
                Raw = json,
                Data = MongoMessageStore.ToBson(data),
            };
            _unsentTimeSeries.Add(TimeSeriesWriter.ToDocument(message, key.GatewayId));
            _unpublished.Add(message);
        }

        if (window.Count > 0)
            _logger.LogInformation("Window closed: {Gateways} gateway messages, {Tags} unique tags",
                window.Count, window.Values.Sum(tags => tags.Count));

        await WriteAsync(_timeSeries.Collection, _unsentTimeSeries, "time-series documents", ct, _timeSeries.EnsureAsync);
        await PublishAsync();
    }

    private async Task PublishAsync()
    {
        if (_unpublished.Count > MaxUnsent)
        {
            _logger.LogWarning("Dropping {Count} oldest unpublished gateway messages", _unpublished.Count - MaxUnsent);
            _unpublished.RemoveRange(0, _unpublished.Count - MaxUnsent);
        }

        var published = 0;
        try
        {
            // Raw holds the gateway message JSON.
            for (; published < _unpublished.Count; published++)
                await _publisher.PublishAsync(_unpublished[published], _unpublished[published].Raw);
            if (published > 0)
                _logger.LogInformation("Published {Count} gateway messages to Redis", published);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish {Count} gateway messages to Redis, retrying next window",
                _unpublished.Count - published);
        }
        _unpublished.RemoveRange(0, published);
    }

    private async Task WriteAsync<T>(IMongoCollection<T> collection, List<T> unsent, string what, CancellationToken ct,
        Func<CancellationToken, Task>? ensureCollection = null)
    {
        if (unsent.Count == 0)
            return;
        if (unsent.Count > MaxUnsent)
        {
            _logger.LogWarning("Dropping {Count} oldest unsent {What}", unsent.Count - MaxUnsent, what);
            unsent.RemoveRange(0, unsent.Count - MaxUnsent);
        }

        try
        {
            if (ensureCollection != null)
                await ensureCollection(ct);
            await collection.InsertManyAsync(unsent, new InsertManyOptions { IsOrdered = false }, ct);
            _logger.LogInformation("Stored {Count} {What}", unsent.Count, what);
            unsent.Clear();
        }
        catch (MongoBulkWriteException<T> ex)
        {
            // Keep only the documents that really failed; duplicate-key errors mean an earlier retry already stored them.
            var failed = ex.WriteErrors.Where(e => e.Code != 11000).Select(e => unsent[e.Index]).ToList();
            _logger.LogError(ex, "Failed to store {Count} {What}, retrying next window", failed.Count, what);
            unsent.Clear();
            unsent.AddRange(failed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store {Count} {What}, retrying next window", unsent.Count, what);
        }
    }
}
