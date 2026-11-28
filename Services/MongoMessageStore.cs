using System.Text;
using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Driver;

namespace NetworkServer.Services;

/// <summary>An incoming message, as stored in the time-series collection and published to Redis.</summary>
public class DeviceMessage
{
    public ObjectId Id { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string Source { get; set; } = "";   // "http" or "mqtt"
    public string? DeviceId { get; set; }      // HTTP route device id or MQTT client id
    public string? Topic { get; set; }         // MQTT topic (null for HTTP)
    public string Format { get; set; } = "";   // report | gateway | repeater | json | key-value | delimited | text | empty
    public string Raw { get; set; } = "";      // original ASCII payload
    public BsonValue Data { get; set; } = BsonNull.Value; // parsed payload, stored as real BSON
}

public record IngestResult(string Format, string? Id = null, int? Buffered = null);

/// <summary>
/// Stores every incoming message in the time-series collection (see TimeSeriesWriter) and adds it to the Redis
/// stream (see RedisStreamPublisher). BLE gateway payloads (report / gateway) are the exception: TagAggregator
/// buffers them and writes them per time window, keeping each mac only once.
/// </summary>
public class MongoMessageStore(
    TagAggregator aggregator, TimeSeriesWriter timeSeries, RedisStreamPublisher publisher, FastLanePublisher fastLane,
    ILogger<MongoMessageStore> logger)
{
    public async Task<IngestResult> IngestAsync(string source, string? deviceId, string? topic, byte[] payload,
        CancellationToken ct = default)
    {
        // gateway commands carry the tag connection key: mask it before anything is parsed, stored, published or logged
        if (SecretMasker.IsCommandTopic(topic))
            payload = Encoding.ASCII.GetBytes(SecretMasker.Mask(Encoding.ASCII.GetString(payload)));
        var (format, data) = AsciiPayloadParser.Parse(payload);

        if (GatewayPayload.TryParse(data, out var batch))
        {
            fastLane.Enqueue(source, batch); // fast lane first: it converts the readings before TagAggregator reuses them
            var buffered = aggregator.Add(source, deviceId, topic, batch);
            logger.LogDebug("Buffered {Kind} message from gateway {GatewayId} with {Count} of {Total} tags",
                batch.Kind, batch.GatewayId, buffered, batch.Tags.Count);
            return new IngestResult(batch.Kind, Buffered: buffered);
        }

        var message = new DeviceMessage
        {
            Id = ObjectId.GenerateNewId(),
            ReceivedAt = DateTime.UtcNow,
            Source = source,
            DeviceId = deviceId,
            Topic = topic,
            Format = format,
            Raw = Encoding.ASCII.GetString(payload),
            Data = ToBson(data),
        };

        await timeSeries.EnsureAsync(ct);
        await timeSeries.Collection.InsertOneAsync(TimeSeriesWriter.ToDocument(message, null), cancellationToken: ct);
        try
        {
            await publisher.PublishAsync(message, data?.ToJsonString() ?? "null");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Stored message {Id} in the time-series collection but could not publish it to Redis",
                message.Id);
        }

        logger.LogInformation("Stored {Source} message {Id} (device: {DeviceId}, topic: {Topic}, format: {Format})",
            source, message.Id, deviceId, topic, format);
        return new IngestResult(format, message.Id.ToString());
    }

    /// <summary>Newest messages first, read back from the time-series collection.</summary>
    public async Task<List<DeviceMessage>> ListAsync(string? deviceId, int limit, CancellationToken ct = default)
    {
        var filter = deviceId is null
            ? Builders<BsonDocument>.Filter.Empty
            : Builders<BsonDocument>.Filter.Eq("meta.deviceId", deviceId);

        var docs = await timeSeries.Collection.Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("timestamp"))
            .Limit(limit)
            .ToListAsync(ct);
        return docs.Select(TimeSeriesWriter.FromDocument).ToList();
    }

    // Wrap in a document so objects, arrays and plain values all convert the same way.
    internal static BsonValue ToBson(JsonNode? node) =>
        node is null ? BsonNull.Value : BsonDocument.Parse($"{{\"v\":{node.ToJsonString()}}}")["v"];
}
