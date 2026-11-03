
using ProgressLogEntity = A360.Wip.Domain.Entities.ProgressLog;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public sealed class ProgressLogRepository : MongoRepository<ProgressLogEntity>,
    IProgressLogRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "progress_logs";

    public ProgressLogRepository(IMongoDatabase database)
        : base(database.GetCollection<ProgressLogEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<ProgressLogEntity>> GetByAssetIdAsync(
        string assetId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.AssetId == assetId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProgressLogEntity>> GetByJobIdAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.JobId == jobId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProgressLogEntity>> GetByTaskIdAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.TaskId == taskId)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<ProgressLogEntity>(
                Builders<ProgressLogEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.LogId),
                new CreateIndexOptions
                {
                    Name = "ix_progress_log_client_reference"
                }),

            new CreateIndexModel<ProgressLogEntity>(
                Builders<ProgressLogEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_progress_log_asset"
                }),

            new CreateIndexModel<ProgressLogEntity>(
                Builders<ProgressLogEntity>.IndexKeys
                    .Ascending(x => x.JobId),
                new CreateIndexOptions
                {
                    Name = "ix_progress_log_job"
                }),

            new CreateIndexModel<ProgressLogEntity>(
                Builders<ProgressLogEntity>.IndexKeys
                    .Ascending(x => x.TaskId)
                    .Ascending(x => x.Timestamp),
                new CreateIndexOptions
                {
                    Name = "ix_progress_log_task_timestamp"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
