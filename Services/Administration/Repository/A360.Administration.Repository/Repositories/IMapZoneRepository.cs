using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface IMapZoneRepository : IMongoRepository<MapZone>
{
    Task<MapZone?> GetByZoneIdAsync(string zoneId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MapZone>> GetByFloorPlanAsync(
        string floorPlanId,
        string? clientId,
        string? tenantId,
        CancellationToken cancellationToken = default);

    /// <param name="exceptZoneId">Zone being edited, left out of the check.</param>
    Task<bool> CodeExistsAsync(string floorPlanId, string code, CancellationToken cancellationToken = default, string? exceptZoneId = null);

    /// <summary>Number of zones (not deleted) per floor plan id.</summary>
    Task<Dictionary<string, int>> CountByFloorPlansAsync(IReadOnlyCollection<string> floorPlanIds, CancellationToken cancellationToken = default);

    Task SoftDeleteByFloorPlanAsync(string floorPlanId, string? updatedBy, CancellationToken cancellationToken = default);
}
