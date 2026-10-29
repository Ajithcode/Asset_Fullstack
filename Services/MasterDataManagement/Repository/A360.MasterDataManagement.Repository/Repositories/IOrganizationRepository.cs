using OrganizationEntity = A360.MasterDataManagement.Domain.Entities.Organization;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IOrganizationRepository : IMongoRepository<OrganizationEntity>
{
    Task<IReadOnlyCollection<OrganizationEntity>> SearchAsync(OrganizationFilter filter, CancellationToken cancellationToken = default);

    Task<OrganizationEntity?> GetByNameAsync(string organizationName, string? clientId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<OrganizationEntity>> GetChildrenAsync(string parentOrganizationId, CancellationToken cancellationToken = default);

    Task<bool> HasChildrenAsync(string parentOrganizationId, CancellationToken cancellationToken = default);

    Task UpdateParentOrganizationNameAsync(string parentOrganizationId, string parentOrganizationName, CancellationToken cancellationToken = default);

    Task UpdateBusinessUnitNameAsync(string businessUnitId, string businessUnitName, CancellationToken cancellationToken = default);

    Task RemoveBusinessUnitAsync(string businessUnitId, CancellationToken cancellationToken = default);

    Task UpdateSiteNameAsync(string siteId, string siteName, CancellationToken cancellationToken = default);

    Task RemoveSiteAsync(string siteId, CancellationToken cancellationToken = default);
}

public sealed record OrganizationFilter(
    string? Search,
    string? Type,
    string? Status,
    string? ParentOrganizationId,
    string? BusinessUnit,
    string? Site,
    string? ClientId);
