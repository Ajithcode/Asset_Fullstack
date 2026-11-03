using FloorEntity = A360.MasterDataManagement.Domain.Entities.Floor;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IFloorRepository : IMongoRepository<FloorEntity>
{
    Task<IReadOnlyCollection<FloorEntity>> SearchAsync(FloorFilter filter, CancellationToken cancellationToken = default);

    // Floor codes only need to be unique inside their building ("F1" exists in every building).
    Task<FloorEntity?> GetByCodeAsync(string code, string buildingId, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithSiteAsync(string siteId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithBuildingAsync(string buildingId, CancellationToken cancellationToken = default);

    Task UpdateSiteNameAsync(string siteId, string siteName, CancellationToken cancellationToken = default);

    Task UpdateBuildingNameAsync(string buildingId, string buildingName, CancellationToken cancellationToken = default);
}

public sealed record FloorFilter(
    string? Search,
    string? Status,
    string? SiteId,
    string? BuildingId,
    string? ParentLevel,
    string? ClientId);
