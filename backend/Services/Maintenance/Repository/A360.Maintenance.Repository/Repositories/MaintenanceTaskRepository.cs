
using MaintenanceTaskEntity = A360.Maintenance.Domain.Entities.MaintenanceTask;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public sealed class MaintenanceTaskRepository : MongoRepository<MaintenanceTaskEntity>,
    IMaintenanceTaskRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "maintenance_tasks";

    public MaintenanceTaskRepository(IMongoDatabase database)
        : base(database.GetCollection<MaintenanceTaskEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<MaintenanceTaskEntity>> GetByAssetIdAsync(
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
            new CreateIndexModel<MaintenanceTaskEntity>(
                Builders<MaintenanceTaskEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_maintenance_task_client_asset"
                }),

            new CreateIndexModel<MaintenanceTaskEntity>(
                Builders<MaintenanceTaskEntity>.IndexKeys
                    .Ascending(x => x.Status),
                new CreateIndexOptions
                {
                    Name = "ix_maintenance_task_status"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
