
using StatusMasterEntity = A360.Wip.Domain.Entities.StatusMaster;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public sealed class StatusMasterRepository : MongoRepository<StatusMasterEntity>,
    IStatusMasterRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "wip_status_masters";

    public StatusMasterRepository(IMongoDatabase database)
        : base(database.GetCollection<StatusMasterEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<StatusMasterEntity>> GetByAssetIdAsync(
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
            new CreateIndexModel<StatusMasterEntity>(
                Builders<StatusMasterEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.StatusId),
                new CreateIndexOptions
                {
                    Name = "ix_status_master_client_reference"
                }),

            new CreateIndexModel<StatusMasterEntity>(
                Builders<StatusMasterEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_status_master_asset"
                }),

            new CreateIndexModel<StatusMasterEntity>(
                Builders<StatusMasterEntity>.IndexKeys
                    .Ascending(x => x.AssetId)
                    .Ascending(x => x.StatusCode),
                new CreateIndexOptions
                {
                    Name = "ix_status_master_asset_code",
                    Unique = true
                }),

            new CreateIndexModel<StatusMasterEntity>(
                Builders<StatusMasterEntity>.IndexKeys
                    .Ascending(x => x.AssetId)
                    .Ascending(x => x.SequenceOrder),
                new CreateIndexOptions
                {
                    Name = "ix_status_master_asset_sequence"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
