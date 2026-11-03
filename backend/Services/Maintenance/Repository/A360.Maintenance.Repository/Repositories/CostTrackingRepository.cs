
using CostTrackingEntity = A360.Maintenance.Domain.Entities.CostTracking;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public sealed class CostTrackingRepository : MongoRepository<CostTrackingEntity>,
    ICostTrackingRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "cost_tracking_records";

    public CostTrackingRepository(IMongoDatabase database)
        : base(database.GetCollection<CostTrackingEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<CostTrackingEntity>> GetByAssetIdAsync(
        string assetId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.AssetId == assetId)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<CostTrackingEntity>(
                Builders<CostTrackingEntity>.IndexKeys
                    .Ascending(x => x.ClientId),
                new CreateIndexOptions
                {
                    Name = "ix_cost_tracking_client"
                }),
            new CreateIndexModel<CostTrackingEntity>(
                Builders<CostTrackingEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_cost_tracking_asset"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
