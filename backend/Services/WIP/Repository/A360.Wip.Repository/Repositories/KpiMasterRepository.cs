
using KpiMasterEntity = A360.Wip.Domain.Entities.KpiMaster;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public sealed class KpiMasterRepository : MongoRepository<KpiMasterEntity>,
    IKpiMasterRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "kpi_masters";

    public KpiMasterRepository(IMongoDatabase database)
        : base(database.GetCollection<KpiMasterEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<KpiMasterEntity>> GetByAssetIdAsync(
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
            new CreateIndexModel<KpiMasterEntity>(
                Builders<KpiMasterEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.KpiId),
                new CreateIndexOptions
                {
                    Name = "ix_kpi_master_client_reference"
                }),

            new CreateIndexModel<KpiMasterEntity>(
                Builders<KpiMasterEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_kpi_master_asset"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
