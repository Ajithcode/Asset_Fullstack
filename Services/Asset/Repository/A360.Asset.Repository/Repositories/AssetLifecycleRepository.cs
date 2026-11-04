using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

/// <summary>One lifecycle record per registered asset.</summary>
public sealed class AssetLifecycleRepository
    : MongoRepository<AssetLifecycleRecord>, IAssetLifecycleRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetLifecycle";

    public AssetLifecycleRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetLifecycleRecord>(CollectionName))
    {
    }

    public async Task<AssetLifecycleRecord?> GetByAssetRegistrationIdAsync(string assetRegistrationId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(record => record.AssetRegistrationId == assetRegistrationId.Trim() && !record.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var index = new CreateIndexModel<AssetLifecycleRecord>(
            Builders<AssetLifecycleRecord>.IndexKeys.Ascending(record => record.AssetRegistrationId),
            new CreateIndexOptions { Name = "ux_asset_lifecycle_asset_registration_id", Unique = true });

        await Collection.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
    }
}
