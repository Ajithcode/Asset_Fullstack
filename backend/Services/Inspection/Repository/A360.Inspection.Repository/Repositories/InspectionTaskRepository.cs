using MongoDB.Driver;
using A360.Repository.Repositories;
using InspectionTaskEntity = A360.Inspection.Domain.Entities.InspectionTask;

namespace A360.Inspection.Repository.Repositories;

public sealed class InspectionTaskRepository : MongoRepository<InspectionTaskEntity>, IInspectionTaskRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "inspection_tasks";

    public InspectionTaskRepository(IMongoDatabase database)
        : base(database.GetCollection<InspectionTaskEntity>(CollectionName))
    {
    }

    public async Task<InspectionTaskEntity?> GetByTaskCodeAsync(string taskCode, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(task => task.TaskCode == taskCode)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<InspectionTaskEntity>(
                Builders<InspectionTaskEntity>.IndexKeys.Ascending(task => task.TaskCode),
                new CreateIndexOptions { Name = "ix_inspection_tasks_task_code", Unique = true }),
            new CreateIndexModel<InspectionTaskEntity>(
                Builders<InspectionTaskEntity>.IndexKeys.Ascending(task => task.AssetId),
                new CreateIndexOptions { Name = "ix_inspection_tasks_asset_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
