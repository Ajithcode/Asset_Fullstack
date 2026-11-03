
using SparePartEntity = A360.Maintenance.Domain.Entities.SparePart;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public sealed class SparePartRepository : MongoRepository<SparePartEntity>,
    ISparePartRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "spare_parts";

    public SparePartRepository(IMongoDatabase database)
        : base(database.GetCollection<SparePartEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<SparePartEntity>> GetByAssetIdAsync(
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
            new CreateIndexModel<SparePartEntity>(
                Builders<SparePartEntity>.IndexKeys
                    .Ascending(x => x.ClientId),
                new CreateIndexOptions
                {
                    Name = "ix_spare_part_client"
                }),
            new CreateIndexModel<SparePartEntity>(
                Builders<SparePartEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_spare_part_asset"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
