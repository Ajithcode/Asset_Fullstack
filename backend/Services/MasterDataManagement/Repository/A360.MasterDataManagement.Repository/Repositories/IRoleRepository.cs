using RoleEntity = A360.MasterDataManagement.Domain.Entities.Role;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IRoleRepository : IMongoRepository<RoleEntity>
{
    Task<IReadOnlyCollection<RoleEntity>> SearchAsync(RoleFilter filter, CancellationToken cancellationToken = default);

    Task<RoleEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithPermissionAsync(string permissionId, CancellationToken cancellationToken = default);

    Task UpdatePermissionNameAsync(string permissionId, string permissionName, CancellationToken cancellationToken = default);

    Task<bool> AnyWithModuleAsync(string moduleId, CancellationToken cancellationToken = default);

    Task UpdateModuleNameAsync(string moduleId, string moduleName, CancellationToken cancellationToken = default);
}

public sealed record RoleFilter(
    string? Search,
    string? Status,
    string? Type,
    string? ClientId);
