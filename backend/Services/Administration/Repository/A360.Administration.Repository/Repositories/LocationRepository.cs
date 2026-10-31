using MongoDB.Bson;
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

    public async Task<IReadOnlyCollection<Location>> GetByLocationIdsAsync(
        IReadOnlyCollection<string> locationIds,
        CancellationToken cancellationToken = default)
    {
        if (locationIds.Count == 0)
        {
            return [];
        }

        var builder = Builders<Location>.Filter;
        var filter = builder.In(location => location.LocationId, locationIds)
            & builder.Eq(location => location.IsDeleted, false);

        return await Collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Location>> GetAsync(
        string? tenantId,
        string? clientId,
        string? locationType,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<Location>.Filter;
        var filter = builder.Eq(location => location.IsDeleted, false);

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            filter &= builder.Eq(location => location.TenantId, tenantId);
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            filter &= builder.Eq(location => location.ClientId, clientId);
        }

        if (!string.IsNullOrWhiteSpace(locationType))
        {
            filter &= builder.Regex(location => location.LocationType, ExactIgnoreCase(locationType));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= builder.Regex(location => location.Status, ExactIgnoreCase(status));
        }

        return await Collection
            .Find(filter)
            .SortBy(location => location.LocationName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> LocationCodeExistsAsync(
        string locationCode,
        string? tenantId,
        string? excludeLocationId = null,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<Location>.Filter;
        var filter = builder.Eq(location => location.LocationCode, locationCode)
            & builder.Eq(location => location.IsDeleted, false)
            & builder.Eq(location => location.TenantId, tenantId);

        if (!string.IsNullOrWhiteSpace(excludeLocationId))
        {
            filter &= builder.Ne(location => location.LocationId, excludeLocationId);
        }

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<bool> HasChildrenAsync(string locationId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(location => location.ParentLocationId == locationId && !location.IsDeleted)
            .AnyAsync(cancellationToken);
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

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(value.Trim())}$", "i");
    }
}
