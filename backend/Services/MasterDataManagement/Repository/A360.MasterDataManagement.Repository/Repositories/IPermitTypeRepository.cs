using PermitTypeEntity = A360.MasterDataManagement.Domain.Entities.PermitType;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IPermitTypeRepository : IMongoRepository<PermitTypeEntity>
{
    Task<IReadOnlyCollection<PermitTypeEntity>> SearchAsync(PermitTypeFilter filter, CancellationToken cancellationToken = default);

    Task<PermitTypeEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);
}

public sealed record PermitTypeFilter(
    string? Search,
    string? Status,
    string? Category,
    string? ClientId);
