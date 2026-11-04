using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class MapLibraryZoneRepository
    : MongoRepository<MapLibraryZone>, IMapLibraryZoneRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "administration_map_library_zones";

    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public MapLibraryZoneRepository(IMongoDatabase database)
        : base(database.GetCollection<MapLibraryZone>(CollectionName))
    {
    }

    public async Task<MapLibraryZone?> GetByZoneIdAsync(string zoneId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(zoneId))
        {
            return null;
        }

        return await Collection
            .Find(zone => zone.ZoneId == zoneId && !zone.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ZoneCodeExistsAsync(
        string zoneCode,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(
                zone => zone.ZoneCode == zoneCode && zone.TenantId == tenantId && !zone.IsDeleted,
                new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<MapLibraryZone>(
                Builders<MapLibraryZone>.IndexKeys.Ascending(zone => zone.ZoneId),
                new CreateIndexOptions { Name = "ux_administration_map_library_zones_zone_id", Unique = true }),
            new CreateIndexModel<MapLibraryZone>(
                Builders<MapLibraryZone>.IndexKeys
                    .Ascending(zone => zone.TenantId)
                    .Ascending(zone => zone.ZoneCode),
                new CreateIndexOptions { Name = "ix_administration_map_library_zones_tenant_id_zone_code", Collation = CaseInsensitive }),
            new CreateIndexModel<MapLibraryZone>(
                Builders<MapLibraryZone>.IndexKeys.Ascending(zone => zone.FloorPlanId),
                new CreateIndexOptions { Name = "ix_administration_map_library_zones_floor_plan_id" })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
