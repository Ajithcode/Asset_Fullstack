using System.Text.RegularExpressions;
using ZoneEntity = A360.MasterDataManagement.Domain.Entities.Zone;
using MongoDB.Bson;
using MongoDB.Driver;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public sealed class ZoneRepository : MongoRepository<ZoneEntity>,
    IZoneRepository,
    IMongoIndexConfigurator
{
    public const string CollectionName = "master_zones";

    public ZoneRepository(IMongoDatabase database)
        : base(database.GetCollection<ZoneEntity>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<ZoneEntity>> SearchAsync(
        ZoneFilter filter,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ZoneEntity>.Filter;
        var filters = new List<FilterDefinition<ZoneEntity>>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Search.Trim()), "i");
            filters.Add(builder.Or(
                builder.Regex(x => x.Name, pattern),
                builder.Regex(x => x.Code, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            filters.Add(builder.Regex(x => x.Status, ExactIgnoreCase(filter.Status)));
        }

        if (!string.IsNullOrWhiteSpace(filter.ParentLevel))
        {
            filters.Add(builder.Regex(x => x.ParentLevel, ExactIgnoreCase(filter.ParentLevel)));
        }

        if (!string.IsNullOrWhiteSpace(filter.SiteId))
        {
            filters.Add(builder.Eq(x => x.Site!.Id, filter.SiteId));
        }

        if (!string.IsNullOrWhiteSpace(filter.BuildingId))
        {
            filters.Add(builder.Eq(x => x.BuildingId, filter.BuildingId));
        }

        if (!string.IsNullOrWhiteSpace(filter.FloorId))
        {
            filters.Add(builder.Eq(x => x.FloorId, filter.FloorId));
        }

        if (!string.IsNullOrWhiteSpace(filter.ClientId))
        {
            filters.Add(builder.Eq(x => x.ClientId, filter.ClientId));
        }

        var combined = filters.Count > 0 ? builder.And(filters) : builder.Empty;

        return await Collection
            .Find(combined)
            .SortBy(x => x.BuildingName)
            .ThenBy(x => x.FloorName)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ZoneEntity?> GetByCodeAsync(
        string code,
        string buildingId,
        string? floorId,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<ZoneEntity>.Filter;
        var filter = builder.Regex(x => x.Code, ExactIgnoreCase(code))
            & builder.Eq(x => x.BuildingId, buildingId)
            & builder.Eq(x => x.FloorId, floorId)
            & builder.Eq(x => x.ClientId, clientId);

        return await Collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AnyWithSiteAsync(
        string siteId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.Site!.Id == siteId)
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> AnyWithBuildingAsync(
        string buildingId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.BuildingId == buildingId)
            .AnyAsync(cancellationToken);
    }

    public async Task<bool> AnyWithFloorAsync(
        string floorId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(x => x.FloorId == floorId)
            .AnyAsync(cancellationToken);
    }

    public async Task UpdateSiteNameAsync(
        string siteId,
        string siteName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.Site!.Id == siteId,
            Builders<ZoneEntity>.Update.Set(x => x.Site!.Name, siteName),
            cancellationToken: cancellationToken);
    }

    public async Task UpdateBuildingNameAsync(
        string buildingId,
        string buildingName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.BuildingId == buildingId,
            Builders<ZoneEntity>.Update.Set(x => x.BuildingName, buildingName),
            cancellationToken: cancellationToken);
    }

    public async Task UpdateFloorNameAsync(
        string floorId,
        string floorName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.FloorId == floorId,
            Builders<ZoneEntity>.Update.Set(x => x.FloorName, floorName),
            cancellationToken: cancellationToken);
    }

    public async Task UpdateFloorBuildingAsync(
        string floorId,
        string buildingId,
        string buildingName,
        CancellationToken cancellationToken = default)
    {
        await Collection.UpdateManyAsync(
            x => x.FloorId == floorId,
            Builders<ZoneEntity>.Update
                .Set(x => x.BuildingId, buildingId)
                .Set(x => x.BuildingName, buildingName),
            cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<ZoneEntity>(
                Builders<ZoneEntity>.IndexKeys
                    .Ascending(x => x.ClientId)
                    .Ascending(x => x.BuildingId)
                    .Ascending(x => x.FloorId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions
                {
                    Name = "ix_master_zone_client_building_floor_code"
                }),
            new CreateIndexModel<ZoneEntity>(
                Builders<ZoneEntity>.IndexKeys
                    .Ascending(x => x.BuildingId),
                new CreateIndexOptions
                {
                    Name = "ix_master_zone_building"
                }),
            new CreateIndexModel<ZoneEntity>(
                Builders<ZoneEntity>.IndexKeys
                    .Ascending(x => x.FloorId),
                new CreateIndexOptions
                {
                    Name = "ix_master_zone_floor"
                }),
            new CreateIndexModel<ZoneEntity>(
                Builders<ZoneEntity>.IndexKeys
                    .Ascending(x => x.Site!.Id),
                new CreateIndexOptions
                {
                    Name = "ix_master_zone_site"
                }),
        };

        await Collection.Indexes.CreateManyAsync(
            indexes,
            cancellationToken);
    }

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{Regex.Escape(value.Trim())}$", "i");
    }
}
