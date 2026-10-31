
using PredictiveMaintenanceEntity = A360.Maintenance.Domain.Entities.PredictiveMaintenance;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public sealed class PredictiveMaintenanceRepository : MongoRepository<PredictiveMaintenanceEntity>,
    IPredictiveMaintenanceRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "predictive_maintenance_alerts";

    public PredictiveMaintenanceRepository(IMongoDatabase database)
        : base(database.GetCollection<PredictiveMaintenanceEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<PredictiveMaintenanceEntity>> GetByAssetIdAsync(
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
            new CreateIndexModel<PredictiveMaintenanceEntity>(
                Builders<PredictiveMaintenanceEntity>.IndexKeys
                    .Ascending(x => x.ClientId),
                new CreateIndexOptions
                {
                    Name = "ix_predictive_maintenance_client"
                }),
            new CreateIndexModel<PredictiveMaintenanceEntity>(
                Builders<PredictiveMaintenanceEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_predictive_maintenance_asset"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
