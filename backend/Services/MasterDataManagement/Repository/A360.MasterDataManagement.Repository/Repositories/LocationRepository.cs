using System.Text.RegularExpressions;
using LocationEntity = A360.MasterDataManagement.Domain.Entities.Location;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class LocationRepository : MongoRepository<LocationEntity>,
    ILocationRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_locations";

    public LocationRepository(IMongoDatabase database)
        : base(database.GetCollection<LocationEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<LocationEntity>> SearchAsync(
        LocationFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<LocationEntity>.Filter;
        var filters = new List<FilterDefinition<LocationEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.LocationName, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        AddEqualsIgnoreCase(filters, x => x.Type, filter.Type);
        AddEqualsIgnoreCase(filters, x => x.Building, filter.Building);
        AddEqualsIgnoreCase(filters, x => x.Floor, filter.Floor);
        AddEqualsIgnoreCase(filters, x => x.Zone, filter.Zone);
        AddEqualsIgnoreCase(filters, x => x.Status, filter.Status);

        if (!string.IsNullOrWhiteSpace(filter.ParentLocationId))
        {
            filters.Add(builder.Eq(x => x.ParentLocationId, filter.ParentLocationId));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.LocationName)
            .ToListAsync(cancellationToken);
    }

    public async Task<LocationEntity?> GetByCodeAsync(
        string code,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<LocationEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LocationEntity>> GetChildrenAsync(
        string parentLocationId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.ParentLocationId == parentLocationId)
            .SortBy(x => x.LocationName)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasChildrenAsync(
        string parentLocationId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.ParentLocationId == parentLocationId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateParentLocationNameAsync(
        string parentLocationId,
        string parentLocationName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.ParentLocationId == parentLocationId,
            Builders<LocationEntity>.Update.Set(x => x.ParentLocationName, parentLocationName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<LocationEntity>(
                Builders<LocationEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_location_client_code"
                }),
            new CreateIndexModel<LocationEntity>(
                Builders<LocationEntity>.IndexKeys
                    .Ascending(x => x.ParentLocationId),
                new CreateIndexOptions
                {
                    Name = "ix_master_location_parent"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }

    private static void AddEqualsIgnoreCase(
        List<FilterDefinition<LocationEntity>> filters,
        System.Linq.Expressions.Expression<Func<LocationEntity, object?>> field,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            filters.Add(Builders<LocationEntity>.Filter.Regex(field, ExactIgnoreCase(value)));
        }
    }

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{Regex.Escape(value.Trim())}$", "i");
    }
}
