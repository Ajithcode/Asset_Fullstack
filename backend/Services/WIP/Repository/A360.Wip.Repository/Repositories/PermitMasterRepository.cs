
using PermitMasterEntity = A360.Wip.Domain.Entities.PermitMaster;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public sealed class PermitMasterRepository : MongoRepository<PermitMasterEntity>,
    IPermitMasterRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "permit_masters";

    public PermitMasterRepository(IMongoDatabase database)
        : base(database.GetCollection<PermitMasterEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<PermitMasterEntity>> GetByAssetIdAsync(
        string assetId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(entity => entity.AssetId == assetId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<PermitMasterEntity>> GetByJobIdAsync(
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
            new CreateIndexModel<PermitMasterEntity>(
                Builders<PermitMasterEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.PermitId),
                new CreateIndexOptions
                {
                    Name = "ix_permit_master_client_reference"
                }),

            new CreateIndexModel<PermitMasterEntity>(
                Builders<PermitMasterEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_permit_master_asset"
                }),

            new CreateIndexModel<PermitMasterEntity>(
                Builders<PermitMasterEntity>.IndexKeys
                    .Ascending(x => x.JobId),
                new CreateIndexOptions
                {
                    Name = "ix_permit_master_job"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
