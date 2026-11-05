using MongoDB.Driver;
using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public sealed class MapLayerRepository : MongoRepository<MapLayer>, IMapLayerRepository, IMongoIndexConfigurator
{
    public const string CollectionName = "maps_layers";

    public MapLayerRepository(IMongoDatabase database)
        : base(database.GetCollection<MapLayer>(CollectionName))
    {
    }

    public async Task<IReadOnlyCollection<MapLayer>> GetByFloorPlanAsync(string floorPlanId, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(layer => layer.FloorPlanId == floorPlanId && !layer.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public async Task<MapLayer?> GetByKeyAsync(string floorPlanId, string layerKey, CancellationToken cancellationToken = default)
    {
        return await Collection
            .Find(layer => layer.FloorPlanId == floorPlanId && layer.LayerKey == layerKey)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SoftDeleteByFloorPlanAsync(string floorPlanId, string? updatedBy, CancellationToken cancellationToken = default)
    {
        var filter = Builders<MapLayer>.Filter.Eq(layer => layer.FloorPlanId, floorPlanId)
            & Builders<MapLayer>.Filter.Eq(layer => layer.IsDeleted, false);
        var update = Builders<MapLayer>.Update
            .Set(layer => layer.IsDeleted, true)
            .Set(layer => layer.UpdatedBy, updatedBy)
            .Set(layer => layer.UpdatedAt, DateTime.UtcNow);

        await Collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task CreateIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<MapLayer>(
                Builders<MapLayer>.IndexKeys
                    .Ascending(layer => layer.FloorPlanId)
                    .Ascending(layer => layer.LayerKey),
                new CreateIndexOptions { Name = "ux_maps_layers_floor_plan_id_layer_key", Unique = true })
        };

        await Collection.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
