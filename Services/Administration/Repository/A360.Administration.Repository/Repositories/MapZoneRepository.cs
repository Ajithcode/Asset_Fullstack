using MongoDB.Bson;
using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class MapZoneRepository : MongoRepository<MapZone>, IMapZoneRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "maps_zones";

    public MapZoneRepository(IMongoDatabase database)
        : base(database.GetCollection<MapZone>(CollectionName))
    {
    }

    public async Task<MapZone?> GetByZoneIdAsync(string zoneId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(zoneId))
        {
            return null;
        }

        return await Collection
            .Find(zone => zone.ZoneId == zoneId && !zone.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<MapZone>> GetByFloorPlanAsync(
        string floorPlanId,
        string? clientId,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        var builder = Builders<MapZone>.Filter;
        var filter = builder.Eq(zone => zone.FloorPlanId, floorPlanId) & builder.Eq(zone => zone.IsDeleted, false);

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            filter &= builder.Eq(zone => zone.ClientId, clientId);
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            filter &= builder.Eq(zone => zone.TenantId, tenantId);
        }

        return await Collection
            .Find(filter)
            .SortBy(zone => zone.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(string floorPlanId, string code, CancellationToken cancellationToken = default, string? exceptZoneId = null)
    {
        var builder = Builders<MapZone>.Filter;
        var filter = builder.Eq(zone => zone.FloorPlanId, floorPlanId)
            & builder.Regex(zone => zone.Code, ExactIgnoreCase(code))
            & builder.Eq(zone => zone.IsDeleted, false);
        if (!string.IsNullOrWhiteSpace(exceptZoneId))
        {
            filter &= builder.Ne(zone => zone.ZoneId, exceptZoneId);
        }

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<Dictionary<string, int>> CountByFloorPlansAsync(IReadOnlyCollection<string> floorPlanIds, CancellationToken cancellationToken = default)
    {
        if (floorPlanIds.Count == 0)
        {
            return [];
        }

        var builder = Builders<MapZone>.Filter;
        var filter = builder.In(zone => zone.FloorPlanId, floorPlanIds) & builder.Eq(zone => zone.IsDeleted, false);

        var planIds = await Collection.Find(filter).Project(zone => zone.FloorPlanId).ToListAsync(cancellationToken);
        return planIds.GroupBy(id => id).ToDictionary(group => group.Key, group => group.Count());
    }

    public async Task SoftDeleteByFloorPlanAsync(string floorPlanId, string? updatedBy, CancellationToken cancellationToken = default)
    {
        var filter = Builders<MapZone>.Filter.Eq(zone => zone.FloorPlanId, floorPlanId)
            & Builders<MapZone>.Filter.Eq(zone => zone.IsDeleted, false);
        var update = Builders<MapZone>.Update
            .Set(zone => zone.IsDeleted, true)
            .Set(zone => zone.UpdatedBy, updatedBy)
            .Set(zone => zone.UpdatedAt, DateTime.UtcNow);

        await Collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<MapZone>(
                Builders<MapZone>.IndexKeys.Ascending(zone => zone.ZoneId),
                new CreateIndexOptions { Name = "ux_maps_zones_zone_id", Unique = true }),
            new CreateIndexModel<MapZone>(
                Builders<MapZone>.IndexKeys
                    .Ascending(zone => zone.FloorPlanId)
                    .Ascending(zone => zone.Code),
                new CreateIndexOptions { Name = "ix_maps_zones_floor_plan_id_code" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private static BsonRegularExpression ExactIgnoreCase(string value)
    {
        return new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(value.Trim())}$", "i");
    }
}
