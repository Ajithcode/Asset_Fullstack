using ZoneEntity = A360.MasterDataManagement.Domain.Entities.Zone;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IZoneRepository : IMongoRepository<ZoneEntity>
{
    Task<IReadOnlyCollection<ZoneEntity>> SearchAsync(ZoneFilter filter, CancellationToken cancellationToken = default);

    // Zone codes only need to be unique inside their floor (or building, for zones without a floor).
    Task<ZoneEntity?> GetByCodeAsync(string code, string buildingId, string? floorId, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithSiteAsync(string siteId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithBuildingAsync(string buildingId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithFloorAsync(string floorId, CancellationToken cancellationToken = default);

    Task UpdateSiteNameAsync(string siteId, string siteName, CancellationToken cancellationToken = default);

    Task UpdateBuildingNameAsync(string buildingId, string buildingName, CancellationToken cancellationToken = default);

    Task UpdateFloorNameAsync(string floorId, string floorName, CancellationToken cancellationToken = default);

    // Keeps zones under a floor that was moved to another building pointing at that building.
    Task UpdateFloorBuildingAsync(string floorId, string buildingId, string buildingName, CancellationToken cancellationToken = default);
}

public sealed record ZoneFilter(
    string? Search,
    string? Status,
    string? SiteId,
    string? BuildingId,
    string? FloorId,
    string? ParentLevel,
    string? ClientId);
