using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface IFloorPlanRepository : IMongoRepository<FloorPlan>
{
    Task<FloorPlan?> GetByFloorPlanIdAsync(string floorPlanId, CancellationToken cancellationToken = default);

    /// <summary>True when a floor plan with this name and version already exists in the tenant, ignoring case.</summary>
    Task<bool> NameVersionExistsAsync(string name, string version, string? tenantId, CancellationToken cancellationToken = default);
}
