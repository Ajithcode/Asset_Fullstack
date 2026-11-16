using System.Text;
using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace NetworkServer.Services;

public class DeviceMessage
{
    [BsonId] public ObjectId Id { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string Source { get; set; } = "";   // "http" or "mqtt"
    public string? DeviceId { get; set; }      // HTTP route device id or MQTT client id
    public string? Topic { get; set; }         // MQTT topic (null for HTTP)
    public string Format { get; set; } = "";   // report | gateway | json | key-value | delimited | text | empty
    public string Raw { get; set; } = "";      // original ASCII payload
    public BsonValue Data { get; set; } = BsonNull.Value; // parsed payload, stored as real BSON
}

public record IngestResult(string Format, string? Id = null, int? Buffered = null);

/// <summary>
/// Stores every incoming message as a document in MongoDB ({Mongo:Database}.{Mongo:Collection}).
/// BLE gateway payloads (report / gateway) are the exception: TagAggregator buffers them and writes them here per
/// time window, keeping each mac only once.
/// The database and collection are created automatically on first insert.
/// Every stored message is also copied to the time-series collection (see TimeSeriesWriter) and added to the Redis
/// stream (see RedisStreamPublisher).
/// </summary>
public class MongoMessageStore
{
    private readonly IMongoCollection<DeviceMessage> _collection;
    private readonly TagAggregator _aggregator;
    private readonly TimeSeriesWriter _timeSeries;
    private readonly RedisStreamPublisher _publisher;
    private readonly ILogger<MongoMessageStore> _logger;

    public MongoMessageStore(IMongoDatabase database, IConfiguration config, TagAggregator aggregator,
        TimeSeriesWriter timeSeries, RedisStreamPublisher publisher, ILogger<MongoMessageStore> logger)
    {
        _timeSeries = timeSeries;
        _publisher = publisher;
        _collection = database.GetCollection<DeviceMessage>(config["Mongo:Collection"] ?? "DeviceMessages");
        _aggregator = aggregator;
        _logger = logger;

        _collection.Indexes.CreateMany([
            new CreateIndexModel<DeviceMessage>(Builders<DeviceMessage>.IndexKeys.Descending(m => m.ReceivedAt)),
            new CreateIndexModel<DeviceMessage>(Builders<DeviceMessage>.IndexKeys
                .Ascending(m => m.DeviceId).Descending(m => m.ReceivedAt)),
        ]);
    }

    public async Task<IngestResult> IngestAsync(string source, string? deviceId, string? topic, byte[] payload,
        CancellationToken ct = default)
    {
        var (format, data) = AsciiPayloadParser.Parse(payload);

        if (GatewayPayload.TryParse(data, out var batch))
        {
            var buffered = _aggregator.Add(source, deviceId, topic, batch);
            _logger.LogDebug("Buffered {Kind} message from gateway {GatewayId} with {Count} of {Total} tags",
                batch.Kind, batch.GatewayId, buffered, batch.Tags.Count);
            return new IngestResult(batch.Kind, Buffered: buffered);
        }

        var message = new DeviceMessage
        {
            ReceivedAt = DateTime.UtcNow,
            Source = source,
            DeviceId = deviceId,
            Topic = topic,
            Format = format,
            Raw = Encoding.ASCII.GetString(payload),
            Data = ToBson(data),
        };

        await _collection.InsertOneAsync(message, cancellationToken: ct);
        try
        {
            await _timeSeries.EnsureAsync(ct);
            await _timeSeries.Collection.InsertOneAsync(TimeSeriesWriter.ToDocument(message, null), cancellationToken: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Stored message {Id} in DeviceMessages but not in the time-series collection", message.Id);
        }
        try
        {
            await _publisher.PublishAsync(message, data?.ToJsonString() ?? "null");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Stored message {Id} in DeviceMessages but could not publish it to Redis", message.Id);
        }

        _logger.LogInformation("Stored {Source} message {Id} (device: {DeviceId}, topic: {Topic}, format: {Format})",
            source, message.Id, deviceId, topic, format);
        return new IngestResult(format, message.Id.ToString());
    }

    public Task<List<DeviceMessage>> ListAsync(string? deviceId, int limit, CancellationToken ct = default)
    {
        var filter = deviceId is null
            ? Builders<DeviceMessage>.Filter.Empty
            : Builders<DeviceMessage>.Filter.Eq(m => m.DeviceId, deviceId);

        return _collection.Find(filter).SortByDescending(m => m.ReceivedAt).Limit(limit).ToListAsync(ct);
    }

    // Wrap in a document so objects, arrays and plain values all convert the same way.
    internal static BsonValue ToBson(JsonNode? node) =>
        node is null ? BsonNull.Value : BsonDocument.Parse($"{{\"v\":{node.ToJsonString()}}}")["v"];
}
