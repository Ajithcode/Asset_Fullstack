using BuildingEntity = A360.MasterDataManagement.Domain.Entities.Building;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IBuildingRepository : IMongoRepository<BuildingEntity>
{
    Task<IReadOnlyCollection<BuildingEntity>> SearchAsync(BuildingFilter filter, CancellationToken cancellationToken = default);

    Task<BuildingEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithSiteAsync(string siteId, CancellationToken cancellationToken = default);

    Task UpdateSiteNameAsync(string siteId, string siteName, CancellationToken cancellationToken = default);
}

public sealed record BuildingFilter(
    string? Search,
    string? Status,
    string? SiteId,
    string? ParentLevel,
    string? ClientId);
