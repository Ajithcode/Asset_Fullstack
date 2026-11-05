using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface IFloorPlanRepository : IMongoRepository<FloorPlan>
{
    Task<FloorPlan?> GetByFloorPlanIdAsync(string floorPlanId, CancellationToken cancellationToken = default);

    /// <summary>Floor plans that are not deleted; every filter applies only when given. Search matches name, description, file name and location path.</summary>
    Task<IReadOnlyCollection<FloorPlan>> GetAsync(
        string? locationId,
        string? status,
        string? planType,
        string? search,
        string? clientId,
        string? tenantId,
        CancellationToken cancellationToken = default);
}
