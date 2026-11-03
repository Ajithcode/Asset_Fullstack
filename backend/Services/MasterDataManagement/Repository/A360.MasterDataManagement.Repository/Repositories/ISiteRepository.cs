using SiteEntity = A360.MasterDataManagement.Domain.Entities.Site;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface ISiteRepository : IMongoRepository<SiteEntity>
{
    Task<IReadOnlyCollection<SiteEntity>> SearchAsync(SiteFilter filter, CancellationToken cancellationToken = default);

    Task<SiteEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithBusinessUnitAsync(string businessUnitId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithDepartmentAsync(string departmentId, CancellationToken cancellationToken = default);

    Task UpdateBusinessUnitNameAsync(string businessUnitId, string businessUnitName, CancellationToken cancellationToken = default);

    Task UpdateDepartmentNameAsync(string departmentId, string departmentName, CancellationToken cancellationToken = default);
}

public sealed record SiteFilter(
    string? Search,
    string? Status,
    string? SiteType,
    string? City,
    string? Country,
    string? BusinessUnitId,
    string? DepartmentId,
    string? ClientId);
