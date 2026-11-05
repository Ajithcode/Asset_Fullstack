using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface IMapPlotRepository : IMongoRepository<MapPlot>
{
    /// <summary>Plots (not deleted) on a floor plan; kind filter applies when given.</summary>
    Task<IReadOnlyCollection<MapPlot>> GetByFloorPlanAsync(
        string floorPlanId,
        string? kind,
        string? clientId,
        string? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>The plot of one item on a floor plan, including a removed (soft-deleted) one so it can be placed again.</summary>
    Task<MapPlot?> GetByKeyAsync(string floorPlanId, string kind, string itemId, bool includeDeleted, CancellationToken cancellationToken = default);

    /// <summary>Number of plots (not deleted) per floor plan id.</summary>
    Task<Dictionary<string, int>> CountByFloorPlansAsync(IReadOnlyCollection<string> floorPlanIds, CancellationToken cancellationToken = default);

    /// <summary>Number of plots (not deleted) per kind over the given floor plans.</summary>
    Task<Dictionary<string, int>> CountByKindAsync(IReadOnlyCollection<string> floorPlanIds, CancellationToken cancellationToken = default);

    /// <summary>Takes a deleted zone off the plots that referenced it.</summary>
    Task ClearZoneAsync(string zoneId, string? updatedBy, CancellationToken cancellationToken = default);

    Task SoftDeleteByFloorPlanAsync(string floorPlanId, string? updatedBy, CancellationToken cancellationToken = default);
}
