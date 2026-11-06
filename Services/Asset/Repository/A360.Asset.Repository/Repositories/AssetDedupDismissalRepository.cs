using MongoDB.Driver;
using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed class AssetDedupDismissalRepository
    : MongoRepository<AssetDedupDismissal>, IAssetDedupDismissalRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "AssetDedupDismissal";

    public AssetDedupDismissalRepository(IMongoDatabase database)
        : base(database.GetCollection<AssetDedupDismissal>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<AssetDedupDismissal>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(dismissal => !dismissal.IsDeleted)
            .SortByDescending(dismissal => dismissal.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<AssetDedupDismissal?> GetByPairKeyAsync(string pairKey, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(dismissal => dismissal.PairKey == pairKey && !dismissal.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AssetDedupDismissal>(
                Builders<AssetDedupDismissal>.IndexKeys.Ascending(dismissal => dismissal.PairKey),
                new CreateIndexOptions { Name = "ix_asset_dedup_dismissal_pair_key" }),
            new CreateIndexModel<AssetDedupDismissal>(
                Builders<AssetDedupDismissal>.IndexKeys.Descending(dismissal => dismissal.CreatedAt),
                new CreateIndexOptions { Name = "ix_asset_dedup_dismissal_created_at" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
