using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface IMapLayerRepository : IMongoRepository<MapLayer>
{
    Task<MapLayer?> GetByLayerIdAsync(string layerId, CancellationToken cancellationToken = default);

    /// <summary>True when the floor plan (or, with no floor plan, the tenant) already has a layer with this name, ignoring case.</summary>
    Task<bool> LayerNameExistsAsync(string layerName, string? floorPlanId, string? tenantId, CancellationToken cancellationToken = default);
}
