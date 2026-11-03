
using JobMasterEntity = A360.Wip.Domain.Entities.JobMaster;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public sealed class JobMasterRepository : MongoRepository<JobMasterEntity>,
    IJobMasterRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "job_masters";

    public JobMasterRepository(IMongoDatabase database)
        : base(database.GetCollection<JobMasterEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<JobMasterEntity>> GetByAssetIdAsync(
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
            new CreateIndexModel<JobMasterEntity>(
                Builders<JobMasterEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.JobId),
                new CreateIndexOptions
                {
                    Name = "ix_job_master_client_reference"
                }),

            new CreateIndexModel<JobMasterEntity>(
                Builders<JobMasterEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_job_master_asset"
                }),

            new CreateIndexModel<JobMasterEntity>(
                Builders<JobMasterEntity>.IndexKeys
                    .Ascending(x => x.Status)
                    .Ascending(x => x.Priority),
                new CreateIndexOptions
                {
                    Name = "ix_job_master_status_priority"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
