
using SlaMasterEntity = A360.Wip.Domain.Entities.SlaMaster;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public sealed class SlaMasterRepository : MongoRepository<SlaMasterEntity>,
    ISlaMasterRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "sla_masters";

    public SlaMasterRepository(IMongoDatabase database)
        : base(database.GetCollection<SlaMasterEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<SlaMasterEntity>> GetByAssetIdAsync(
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
            new CreateIndexModel<SlaMasterEntity>(
                Builders<SlaMasterEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.SlaId),
                new CreateIndexOptions
                {
                    Name = "ix_sla_master_client_reference"
                }),

            new CreateIndexModel<SlaMasterEntity>(
                Builders<SlaMasterEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_sla_master_asset"
                }),

            new CreateIndexModel<SlaMasterEntity>(
                Builders<SlaMasterEntity>.IndexKeys
                    .Ascending(x => x.WorkType)
                    .Ascending(x => x.Priority),
                new CreateIndexOptions
                {
                    Name = "ix_sla_master_work_type_priority"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
