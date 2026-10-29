using BusinessUnitEntity = A360.MasterDataManagement.Domain.Entities.BusinessUnit;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IBusinessUnitRepository : IMongoRepository<BusinessUnitEntity>
{
    Task<IReadOnlyCollection<BusinessUnitEntity>> SearchAsync(BusinessUnitFilter filter, CancellationToken cancellationToken = default);

    Task<BusinessUnitEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithParentOrganizationAsync(string organizationId, CancellationToken cancellationToken = default);

    Task UpdateParentOrganizationNameAsync(string organizationId, string organizationName, CancellationToken cancellationToken = default);

    Task UpdateDepartmentNameAsync(string departmentId, string departmentName, CancellationToken cancellationToken = default);

    Task RemoveDepartmentAsync(string departmentId, CancellationToken cancellationToken = default);

    Task UpdateSiteNameAsync(string siteId, string siteName, CancellationToken cancellationToken = default);

    Task RemoveSiteAsync(string siteId, CancellationToken cancellationToken = default);
}

public sealed record BusinessUnitFilter(
    string? Search,
    string? Status,
    string? ParentOrganization,
    string? Department,
    string? Site,
    string? ClientId);
