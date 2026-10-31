
using TaskMasterEntity = A360.Wip.Domain.Entities.TaskMaster;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public sealed class TaskMasterRepository : MongoRepository<TaskMasterEntity>,
    ITaskMasterRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "task_masters";

    public TaskMasterRepository(IMongoDatabase database)
        : base(database.GetCollection<TaskMasterEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<TaskMasterEntity>> GetByAssetIdAsync(
        string assetId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.AssetId == assetId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<TaskMasterEntity>> GetByJobIdAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.JobId == jobId)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<TaskMasterEntity>(
                Builders<TaskMasterEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.TaskId),
                new CreateIndexOptions
                {
                    Name = "ix_task_master_client_reference"
                }),

            new CreateIndexModel<TaskMasterEntity>(
                Builders<TaskMasterEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_task_master_asset"
                }),

            new CreateIndexModel<TaskMasterEntity>(
                Builders<TaskMasterEntity>.IndexKeys
                    .Ascending(x => x.JobId),
                new CreateIndexOptions
                {
                    Name = "ix_task_master_job"
                }),

            new CreateIndexModel<TaskMasterEntity>(
                Builders<TaskMasterEntity>.IndexKeys
                    .Ascending(x => x.AssetId)
                    .Ascending(x => x.SequenceOrder),
                new CreateIndexOptions
                {
                    Name = "ix_task_master_asset_sequence"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
