using System.Text.Json;
using System.Threading.Channels;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;

namespace A360.Tracking.Api.Locating;

/// <summary>One zone / device change published by the worker's fast resolver (Redis pub/sub, channel RealTimeLocation:PushChannel).</summary>
public sealed record LocationPush(string Id, string Tag, string Kind, string? ZoneId, string? ZoneName, string? Device, long DecidedMs);

/// <summary>
/// Live push for the tracking WebSocket: the worker publishes every zone change on a Redis channel; this hub fans the message out
/// to the open sockets, which then send the change at once instead of waiting for their 5 s poll (the poll stays as reconcile).
/// Off (no subscription, sockets only poll) when Redis:ConnectionString is not set or Redis is unreachable.
/// Also stamps wsSentMs on the LocationLatency row of a change when the first socket has sent it (dev latency page).
/// </summary>
public sealed class LocationPushHub : IHostedService
{
    private readonly IConfiguration _config;
    private readonly IMongoDatabase _database;
    private readonly ILogger<LocationPushHub> _logger;
    private readonly List<Channel<LocationPush>> _subscribers = [];
    private ConnectionMultiplexer? _redis;

    public LocationPushHub(IConfiguration config, IMongoDatabase database, ILogger<LocationPushHub> logger)
    {
        _config = config;
        _database = database;
        _logger = logger;
    }

    public bool Enabled => _redis is { IsConnected: true };

    public Channel<LocationPush> Subscribe()
    {
        var channel = Channel.CreateBounded<LocationPush>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.DropOldest });
        lock (_subscribers) _subscribers.Add(channel);
        return channel;
    }

    public void Unsubscribe(Channel<LocationPush> channel)
    {
        lock (_subscribers) _subscribers.Remove(channel);
        channel.Writer.TryComplete();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var connection = _config["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connection))
        {
            _logger.LogInformation("Location push is off (Redis:ConnectionString not set); sockets poll only");
            return;
        }
        try
        {
            var options = ConfigurationOptions.Parse(connection);
            options.AbortOnConnectFail = false; // keep retrying; sockets keep polling meanwhile
            _redis = await ConnectionMultiplexer.ConnectAsync(options);
            var channelName = _config["RealTimeLocation:PushChannel"] ?? "a360:tag_location";
            await _redis.GetSubscriber().SubscribeAsync(RedisChannel.Literal(channelName), (_, message) => OnMessage(message));
            _logger.LogInformation("Location push on: Redis channel {Channel}", channelName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Location push could not start; sockets poll only");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _redis?.Dispose();
        return Task.CompletedTask;
    }

    private void OnMessage(RedisValue message)
    {
        try
        {
            using var doc = JsonDocument.Parse(message.ToString());
            var r = doc.RootElement;
            string? Str(string n) => r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            var push = new LocationPush(Str("id") ?? "", Str("tag") ?? "", Str("kind") ?? "", Str("zoneId"), Str("zoneName"), Str("device"),
                r.TryGetProperty("decidedMs", out var d) && d.TryGetInt64(out var ms) ? ms : 0);
            lock (_subscribers)
                foreach (var s in _subscribers) s.Writer.TryWrite(push);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ignoring a malformed location push");
        }
    }

    /// <summary>Latency stage "WebSocket sent": only the first socket to send a change stamps it.</summary>
    public async Task MarkSentAsync(IEnumerable<string> ids)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var id in ids.Where(i => ObjectId.TryParse(i, out _)))
        {
            try
            {
                await _database.GetCollection<BsonDocument>("LocationLatency").UpdateOneAsync(
                    new BsonDocument { { "_id", ObjectId.Parse(id) }, { "wsSentMs", BsonNull.Value } },
                    new BsonDocument("$set", new BsonDocument("wsSentMs", now)));
            }
            catch (Exception ex) { _logger.LogDebug(ex, "Could not stamp wsSentMs"); }
        }
    }
}
