
using LocationMasterEntity = A360.Wip.Domain.Entities.LocationMaster;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public sealed class LocationMasterRepository : MongoRepository<LocationMasterEntity>,
    ILocationMasterRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "location_masters";

    public LocationMasterRepository(IMongoDatabase database)
        : base(database.GetCollection<LocationMasterEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<LocationMasterEntity>> GetByAssetIdAsync(
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
            new CreateIndexModel<LocationMasterEntity>(
                Builders<LocationMasterEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.LocationId),
                new CreateIndexOptions
                {
                    Name = "ix_location_master_client_reference"
                }),

            new CreateIndexModel<LocationMasterEntity>(
                Builders<LocationMasterEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_location_master_asset"
                }),

            new CreateIndexModel<LocationMasterEntity>(
                Builders<LocationMasterEntity>.IndexKeys
                    .Ascending(x => x.ParentLocation),
                new CreateIndexOptions
                {
                    Name = "ix_location_master_parent"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
