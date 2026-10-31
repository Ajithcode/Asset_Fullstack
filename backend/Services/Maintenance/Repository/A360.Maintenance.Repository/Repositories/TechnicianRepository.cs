
using TechnicianEntity = A360.Maintenance.Domain.Entities.Technician;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Maintenance.Repository.Repositories;

public sealed class TechnicianRepository : MongoRepository<TechnicianEntity>,
    ITechnicianRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "technicians";

    public TechnicianRepository(IMongoDatabase database)
        : base(database.GetCollection<TechnicianEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<TechnicianEntity>> GetByAssetIdAsync(
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
            new CreateIndexModel<TechnicianEntity>(
                Builders<TechnicianEntity>.IndexKeys
                    .Ascending(x => x.ClientId),
                new CreateIndexOptions
                {
                    Name = "ix_technician_client"
                }),
            new CreateIndexModel<TechnicianEntity>(
                Builders<TechnicianEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_technician_asset"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
