using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface IMapLayerRepository : IMongoRepository<MapLayer>
{
    /// <summary>Stored layer options (not deleted) of a floor plan.</summary>
    Task<IReadOnlyCollection<MapLayer>> GetByFloorPlanAsync(string floorPlanId, CancellationToken cancellationToken = default);

    /// <summary>The stored row of one layer, including a soft-deleted one so the unique (plan, key) row is reused.</summary>
    Task<MapLayer?> GetByKeyAsync(string floorPlanId, string layerKey, CancellationToken cancellationToken = default);

    Task SoftDeleteByFloorPlanAsync(string floorPlanId, string? updatedBy, CancellationToken cancellationToken = default);
}
