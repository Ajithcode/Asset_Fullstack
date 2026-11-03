using PermissionEntity = A360.MasterDataManagement.Domain.Entities.Permission;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IPermissionRepository : IMongoRepository<PermissionEntity>
{
    Task<IReadOnlyCollection<PermissionEntity>> SearchAsync(PermissionFilter filter, CancellationToken cancellationToken = default);

    Task<PermissionEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithModuleAsync(string moduleId, CancellationToken cancellationToken = default);

    Task UpdateModuleNameAsync(string moduleId, string moduleName, CancellationToken cancellationToken = default);
}

public sealed record PermissionFilter(
    string? Search,
    string? Status,
    string? ModuleId,
    string? Type,
    string? ClientId);
