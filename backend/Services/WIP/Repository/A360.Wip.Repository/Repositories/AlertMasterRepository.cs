
using AlertMasterEntity = A360.Wip.Domain.Entities.AlertMaster;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public sealed class AlertMasterRepository : MongoRepository<AlertMasterEntity>,
    IAlertMasterRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "alert_masters";

    public AlertMasterRepository(IMongoDatabase database)
        : base(database.GetCollection<AlertMasterEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<AlertMasterEntity>> GetByAssetIdAsync(
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
            new CreateIndexModel<AlertMasterEntity>(
                Builders<AlertMasterEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.AlertId),
                new CreateIndexOptions
                {
                    Name = "ix_alert_master_client_reference"
                }),

            new CreateIndexModel<AlertMasterEntity>(
                Builders<AlertMasterEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_alert_master_asset"
                }),

            new CreateIndexModel<AlertMasterEntity>(
                Builders<AlertMasterEntity>.IndexKeys
                    .Ascending(x => x.AlertType),
                new CreateIndexOptions
                {
                    Name = "ix_alert_master_type"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
