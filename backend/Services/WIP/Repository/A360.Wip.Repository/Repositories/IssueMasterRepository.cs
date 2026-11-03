
using IssueMasterEntity = A360.Wip.Domain.Entities.IssueMaster;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public sealed class IssueMasterRepository : MongoRepository<IssueMasterEntity>,
    IIssueMasterRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "issue_masters";

    public IssueMasterRepository(IMongoDatabase database)
        : base(database.GetCollection<IssueMasterEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<IssueMasterEntity>> GetByAssetIdAsync(
        string assetId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.AssetId == assetId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<IssueMasterEntity>> GetByJobIdAsync(
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
            new CreateIndexModel<IssueMasterEntity>(
                Builders<IssueMasterEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.IssueId),
                new CreateIndexOptions
                {
                    Name = "ix_issue_master_client_reference"
                }),

            new CreateIndexModel<IssueMasterEntity>(
                Builders<IssueMasterEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_issue_master_asset"
                }),

            new CreateIndexModel<IssueMasterEntity>(
                Builders<IssueMasterEntity>.IndexKeys
                    .Ascending(x => x.JobId),
                new CreateIndexOptions
                {
                    Name = "ix_issue_master_job"
                }),

            new CreateIndexModel<IssueMasterEntity>(
                Builders<IssueMasterEntity>.IndexKeys
                    .Ascending(x => x.TaskId),
                new CreateIndexOptions
                {
                    Name = "ix_issue_master_task"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
