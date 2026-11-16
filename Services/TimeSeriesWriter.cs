using MongoDB.Bson;
using MongoDB.Driver;

namespace NetworkServer.Services;

/// <summary>
/// Writes every DeviceMessage (the only Mongo copy of it) to the time-series collection {Mongo:TimeSeriesCollection}
/// (timeField "timestamp", metaField "meta"):
///   { timestamp, meta: { gatewayid, source, deviceId, topic, format }, Raw, Data }
/// An insert into a missing collection would create a normal collection, so callers run <see cref="EnsureAsync"/>
/// before inserting: if the collection was dropped, it is recreated as a time-series collection.
/// </summary>
public class TimeSeriesWriter(IMongoDatabase database, IConfiguration config, ILogger<TimeSeriesWriter> logger)
{
    private readonly string _name = config["Mongo:TimeSeriesCollection"] ?? "GatewayTimeSeries";

    public IMongoCollection<BsonDocument> Collection => database.GetCollection<BsonDocument>(_name);

    public async Task EnsureAsync(CancellationToken ct)
    {
        var filter = new BsonDocument("name", _name);
        var existing = await (await database.ListCollectionsAsync(new ListCollectionsOptions { Filter = filter }, ct))
            .FirstOrDefaultAsync(ct);
        if (existing != null)
        {
            if (existing.GetValue("type", "collection") != "timeseries")
                throw new InvalidOperationException($"Collection '{_name}' exists but is not a time-series collection.");
            return;
        }

        try
        {
            await database.CreateCollectionAsync(_name, new CreateCollectionOptions
            {
                TimeSeriesOptions = new TimeSeriesOptions("timestamp", "meta", TimeSeriesGranularity.Seconds),
            }, ct);
            logger.LogInformation("Created time-series collection {Collection}", _name);
        }
        catch (MongoCommandException ex) when (ex.Code == 48)
        {
            // NamespaceExists: another insert path created it at the same moment.
        }
    }

    public static BsonDocument ToDocument(DeviceMessage message, string? gatewayId) => new()
    {
        { "_id", message.Id },
        { "timestamp", message.ReceivedAt },
        { "meta", new BsonDocument
            {
                { "gatewayid", (BsonValue?)gatewayId ?? BsonNull.Value },
                { "source", message.Source },
                { "deviceId", (BsonValue?)message.DeviceId ?? BsonNull.Value },
                { "topic", (BsonValue?)message.Topic ?? BsonNull.Value },
                { "format", message.Format },
            }
        },
        { "Raw", message.Raw },
        { "Data", message.Data },
    };

    public static DeviceMessage FromDocument(BsonDocument doc)
    {
        var meta = doc.GetValue("meta", new BsonDocument()).AsBsonDocument;
        string? Str(BsonDocument d, string name) => d.TryGetValue(name, out var v) && v.IsString ? v.AsString : null;
        return new DeviceMessage
        {
            Id = doc.GetValue("_id", ObjectId.Empty).IsObjectId ? doc["_id"].AsObjectId : ObjectId.Empty,
            ReceivedAt = doc.GetValue("timestamp", BsonNull.Value).IsValidDateTime ? doc["timestamp"].ToUniversalTime() : default,
            Source = Str(meta, "source") ?? "",
            DeviceId = Str(meta, "deviceId"),
            Topic = Str(meta, "topic"),
            Format = Str(meta, "format") ?? "",
            Raw = Str(doc, "Raw") ?? "",
            Data = doc.GetValue("Data", BsonNull.Value),
        };
    }
}
