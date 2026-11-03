using ModuleAccessEntity = A360.MasterDataManagement.Domain.Entities.ModuleAccess;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IModuleAccessRepository : IMongoRepository<ModuleAccessEntity>
{
    Task<IReadOnlyCollection<ModuleAccessEntity>> SearchAsync(ModuleAccessFilter filter, CancellationToken cancellationToken = default);

    Task<ModuleAccessEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);
}

public sealed record ModuleAccessFilter(
    string? Search,
    string? Status,
    string? Category,
    string? ClientId);
