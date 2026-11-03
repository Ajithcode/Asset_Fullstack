using DepartmentEntity = A360.MasterDataManagement.Domain.Entities.Department;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IDepartmentRepository : IMongoRepository<DepartmentEntity>
{
    Task<IReadOnlyCollection<DepartmentEntity>> SearchAsync(DepartmentFilter filter, CancellationToken cancellationToken = default);

    Task<DepartmentEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithBusinessUnitAsync(string businessUnitId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithParentDepartmentAsync(string parentDepartmentId, CancellationToken cancellationToken = default);

    Task UpdateBusinessUnitNameAsync(string businessUnitId, string businessUnitName, CancellationToken cancellationToken = default);

    Task<bool> AnyWithDepartmentHeadAsync(string employeeId, CancellationToken cancellationToken = default);

    Task UpdateDepartmentHeadNameAsync(string employeeId, string employeeName, CancellationToken cancellationToken = default);
}

public sealed record DepartmentFilter(
    string? Search,
    string? Status,
    string? BusinessUnitId,
    string? DepartmentHead,
    // Limits the search to these departments (e.g. the departments of matching employees) when set.
    IReadOnlyCollection<string>? DepartmentIds,
    string? Asset,
    string? ClientId);
