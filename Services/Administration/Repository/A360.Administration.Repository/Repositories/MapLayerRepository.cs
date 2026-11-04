using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class MapLayerRepository : MongoRepository<MapLayer>, IMapLayerRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "administration_map_layers";

    private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

    public MapLayerRepository(IMongoDatabase database)
        : base(database.GetCollection<MapLayer>(CollectionName))
    {
    }

    public async Task<MapLayer?> GetByLayerIdAsync(string layerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(layerId))
        {
            return null;
        }

        return await Collection
            .Find(layer => layer.LayerId == layerId && !layer.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> LayerNameExistsAsync(
        string layerName,
        string? floorPlanId,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(
                layer => layer.LayerName == layerName
                    && layer.FloorPlanId == floorPlanId
                    && layer.TenantId == tenantId
                    && !layer.IsDeleted,
                new FindOptions { Collation = CaseInsensitive })
            .AnyAsync(cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<MapLayer>(
                Builders<MapLayer>.IndexKeys.Ascending(layer => layer.LayerId),
                new CreateIndexOptions { Name = "ux_administration_map_layers_layer_id", Unique = true }),
            new CreateIndexModel<MapLayer>(
                Builders<MapLayer>.IndexKeys
                    .Ascending(layer => layer.TenantId)
                    .Ascending(layer => layer.FloorPlanId)
                    .Ascending(layer => layer.LayerName),
                new CreateIndexOptions { Name = "ix_administration_map_layers_tenant_id_floor_plan_id_layer_name", Collation = CaseInsensitive })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
