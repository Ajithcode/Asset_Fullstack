using A360.Administration.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Administration.Repository.Repositories;

public interface IRoleRepository : IMongoRepository<Role>
{
    Task<Role?> GetByRoleIdAsync(string roleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Role>> GetByRoleIdsAsync(IReadOnlyCollection<string> roleIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Role>> GetAsync(
        string? tenantId,
        string? clientId,
        string? roleType,
        string? status,
        CancellationToken cancellationToken = default);

    /// <param name="exceptRoleId">Role being edited, left out of the check.</param>
    Task<bool> RoleCodeExistsAsync(string roleCode, string? tenantId, CancellationToken cancellationToken = default, string? exceptRoleId = null);

    /// <summary>Roles (not deleted) with any of the given names, case-insensitive; tenant filter applies when given.</summary>
    Task<IReadOnlyCollection<Role>> GetByNamesAsync(IReadOnlyCollection<string> roleNames, string? tenantId, CancellationToken cancellationToken = default);
}
