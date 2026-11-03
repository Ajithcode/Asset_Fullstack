using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Dashboard.Repository.Repositories;

// Reads device status readings written by the network server; the data is maintained outside this service.
public sealed class NetworkStatusRepository : INetworkStatusRepository
{
    public const string CollectionName = "NetworkTimeSeries";

    // Assets link to a tag through mappeduniqueid; read here only to show the asset for each tag.
    private const string AssetsCollectionName = "assets";

    private const string Online = "online";
    private const string Offline = "offline";

    private readonly IMongoCollection<BsonDocument> _collection;

    public NetworkStatusRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<BsonDocument>(CollectionName);
    }

    public async Task<NetworkStatusCounts> GetStatusCountsAsync(CancellationToken cancellationToken = default)
    {
        // Each tag reports many readings; count every tag once using its latest status.
        // Status is lower-cased so "Online"/"ONLINE" are counted together.
        var results = await _collection
            .Aggregate()
            .Match(Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Type("status", BsonType.String),
                Builders<BsonDocument>.Filter.Exists("meta.tagid")))
            .Sort(Builders<BsonDocument>.Sort.Descending("timestamp"))
            .Group(new BsonDocument
            {
                { "_id", "$meta.tagid" },
                { "status", new BsonDocument("$first", new BsonDocument("$toLower", "$status")) }
            })
            .Group(new BsonDocument
            {
                { "_id", "$status" },
                { "count", new BsonDocument("$sum", 1) }
            })
            .ToListAsync(cancellationToken);

        var counts = results.ToDictionary(
            result => result["_id"].AsString,
            result => result["count"].ToInt64());

        return new NetworkStatusCounts(
            counts.GetValueOrDefault(Online),
            counts.GetValueOrDefault(Offline));
    }

    public async Task<IReadOnlyCollection<TagRecordCount>> GetTagRecordCountsAsync(CancellationToken cancellationToken = default)
    {
        var results = await _collection
            .Aggregate()
            .Match(Builders<BsonDocument>.Filter.Exists("meta.tagid"))
            .Group(new BsonDocument
            {
                { "_id", "$meta.tagid" },
                { "count", new BsonDocument("$sum", 1) }
            })
            .Sort(new BsonDocument { { "count", -1 }, { "_id", 1 } })
            .Lookup(AssetsCollectionName, "_id", "mappeduniqueid", "assets")
            .ToListAsync(cancellationToken);

        return results
            .Select(result =>
            {
                var asset = result["assets"].AsBsonArray.Select(value => value.AsBsonDocument).FirstOrDefault();

                return new TagRecordCount(
                    result["_id"].ToString()!,
                    result["count"].ToInt64(),
                    asset.GetStringOrNull("assetid"),
                    asset.GetStringOrNull("assetname"));
            })
            .ToList();
    }
}
