
using WorkOrderEntity = A360.Maintenance.Domain.Entities.WorkOrder;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public sealed class WorkOrderRepository : MongoRepository<WorkOrderEntity>,
    IWorkOrderRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "work_orders";

    public WorkOrderRepository(IMongoDatabase database)
        : base(database.GetCollection<WorkOrderEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<WorkOrderEntity>> GetByAssetIdAsync(
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
            new CreateIndexModel<WorkOrderEntity>(
                Builders<WorkOrderEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.WorkOrderId),
                new CreateIndexOptions
                {
                    Name = "ix_work_order_client_reference"
                }),

            new CreateIndexModel<WorkOrderEntity>(
                Builders<WorkOrderEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_work_order_asset"
                }),

            new CreateIndexModel<WorkOrderEntity>(
                Builders<WorkOrderEntity>.IndexKeys
                    .Ascending(x => x.Status)
                    .Ascending(x => x.Priority),
                new CreateIndexOptions
                {
                    Name = "ix_work_order_status_priority"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
