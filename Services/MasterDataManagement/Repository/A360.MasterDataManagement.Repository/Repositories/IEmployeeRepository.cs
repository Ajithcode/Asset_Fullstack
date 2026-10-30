using EmployeeEntity = A360.MasterDataManagement.Domain.Entities.Employee;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IEmployeeRepository : IMongoRepository<EmployeeEntity>
{
    Task<IReadOnlyCollection<EmployeeEntity>> SearchAsync(EmployeeFilter filter, CancellationToken cancellationToken = default);

    Task<EmployeeEntity?> GetByEmployeeIdAsync(string employeeId, string? clientId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<EmployeeEntity>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<EmployeeEntity>> GetByDepartmentsAsync(IEnumerable<string> departmentIds, CancellationToken cancellationToken = default);

    // Employees whose record id, employee id or name matches the value (case-insensitive).
    Task<IReadOnlyCollection<EmployeeEntity>> FindByReferenceAsync(string value, CancellationToken cancellationToken = default);

    Task<bool> AnyWithDepartmentAsync(string departmentId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithReportingManagerAsync(string employeeId, CancellationToken cancellationToken = default);

    Task UpdateReportingManagerNameAsync(string employeeId, string employeeName, CancellationToken cancellationToken = default);

    Task UpdateDepartmentNameAsync(string departmentId, string departmentName, CancellationToken cancellationToken = default);

    // Makes the department's employees exactly the given ones: listed employees are moved into it, others it had are cleared.
    Task SetDepartmentEmployeesAsync(string departmentId, string departmentName, IReadOnlyCollection<string> employeeIds, CancellationToken cancellationToken = default);

    // Number of records holding the skill (by name) in the client's data.
    Task<long> CountWithSkillAsync(string skillName, string? clientId, CancellationToken cancellationToken = default);

    // Replaces the skill name wherever it is held, after the skill is renamed.
    Task RenameSkillAsync(string oldName, string newName, string? clientId, CancellationToken cancellationToken = default);
}

public sealed record EmployeeFilter(
    string? Search,
    string? Status,
    string? DepartmentId,
    string? Designation,
    string? ClientId);
