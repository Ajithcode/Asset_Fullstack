using MongoDB.Bson;
using MongoDB.Driver;

namespace A360.Dashboard.Repository.Repositories;

// Reads the assets collection (assetid / assetname / mappeduniqueid documents); the data is maintained outside this service.
public sealed class DashboardAssetRepository : IDashboardAssetRepository
{
    public const string CollectionName = "assets";

    private readonly IMongoCollection<BsonDocument> _collection;

    public DashboardAssetRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<BsonDocument>(CollectionName);
    }

    public async Task<IReadOnlyCollection<DashboardAsset>> GetAsync(CancellationToken cancellationToken = default)
    {
        var assets = await _collection
            .Find(Builders<BsonDocument>.Filter.Exists("assetid"))
            .SortBy(asset => asset["assetname"])
            .ToListAsync(cancellationToken);

        return assets
            .Select(asset => new DashboardAsset(
                asset["_id"].ToString()!,
                asset.GetStringOrNull("assetid"),
                asset.GetStringOrNull("assetname"),
                asset.GetStringOrNull("mappeduniqueid")))
            .ToList();
    }
}
