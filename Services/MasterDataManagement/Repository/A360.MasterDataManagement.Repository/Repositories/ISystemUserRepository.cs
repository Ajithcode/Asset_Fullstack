using SystemUserEntity = A360.MasterDataManagement.Domain.Entities.SystemUser;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface ISystemUserRepository : IMongoRepository<SystemUserEntity>
{
    Task<IReadOnlyCollection<SystemUserEntity>> SearchAsync(SystemUserFilter filter, CancellationToken cancellationToken = default);

    Task<SystemUserEntity?> GetByEmailAsync(string email, string? clientId, CancellationToken cancellationToken = default);

    Task<SystemUserEntity?> GetByUsernameAsync(string username, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithDepartmentAsync(string departmentId, CancellationToken cancellationToken = default);

    Task UpdateDepartmentNameAsync(string departmentId, string departmentName, CancellationToken cancellationToken = default);

    Task<bool> AnyWithPrimaryRoleAsync(string primaryRoleId, CancellationToken cancellationToken = default);

    Task UpdatePrimaryRoleNameAsync(string primaryRoleId, string primaryRoleName, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> CountByPrimaryRoleAsync(IEnumerable<string> primaryRoleIds, CancellationToken cancellationToken = default);
}

public sealed record SystemUserFilter(
    string? Search,
    string? Status,
    string? PrimaryRoleId,
    string? DepartmentId,
    string? ClientId);
