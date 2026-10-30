using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class LocationRepository : MongoRepository<Location>, ILocationRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "organization_locations";

    public LocationRepository(IMongoDatabase database)
        : base(database.GetCollection<Location>(CollectionName))
    {
    }

    public async Task<Location?> GetByLocationIdAsync(string locationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(locationId))
        {
            return null;
        }

        return await Collection
            .Find(location => location.LocationId == locationId && !location.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> LocationCodeExistsAsync(
        string locationCode,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<Location>.Filter;
        var filter = builder.Eq(location => location.LocationCode, locationCode)
            & builder.Eq(location => location.IsDeleted, false)
            & builder.Eq(location => location.TenantId, tenantId);

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<Location>(
                Builders<Location>.IndexKeys.Ascending(location => location.LocationId),
                new CreateIndexOptions { Name = "ux_organization_locations_location_id", Unique = true }),
            new CreateIndexModel<Location>(
                Builders<Location>.IndexKeys
                    .Ascending(location => location.TenantId)
                    .Ascending(location => location.LocationCode),
                new CreateIndexOptions { Name = "ix_organization_locations_tenant_id_location_code" }),
            new CreateIndexModel<Location>(
                Builders<Location>.IndexKeys.Ascending(location => location.ParentLocationId),
                new CreateIndexOptions { Name = "ix_organization_locations_parent_location_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
