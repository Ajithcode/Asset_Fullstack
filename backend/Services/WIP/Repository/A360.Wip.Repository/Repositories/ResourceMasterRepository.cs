
using ResourceMasterEntity = A360.Wip.Domain.Entities.ResourceMaster;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public sealed class ResourceMasterRepository : MongoRepository<ResourceMasterEntity>,
    IResourceMasterRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "resource_masters";

    public ResourceMasterRepository(IMongoDatabase database)
        : base(database.GetCollection<ResourceMasterEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<ResourceMasterEntity>> GetByAssetIdAsync(
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
            new CreateIndexModel<ResourceMasterEntity>(
                Builders<ResourceMasterEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.ResourceId),
                new CreateIndexOptions
                {
                    Name = "ix_resource_master_client_reference"
                }),

            new CreateIndexModel<ResourceMasterEntity>(
                Builders<ResourceMasterEntity>.IndexKeys
                    .Ascending(x => x.AssetId),
                new CreateIndexOptions
                {
                    Name = "ix_resource_master_asset"
                }),

            new CreateIndexModel<ResourceMasterEntity>(
                Builders<ResourceMasterEntity>.IndexKeys
                    .Ascending(x => x.DepartmentId),
                new CreateIndexOptions
                {
                    Name = "ix_resource_master_department"
                }),

            new CreateIndexModel<ResourceMasterEntity>(
                Builders<ResourceMasterEntity>.IndexKeys
                    .Ascending(x => x.AvailabilityStatus),
                new CreateIndexOptions
                {
                    Name = "ix_resource_master_availability"
                })
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }
}
