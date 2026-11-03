using CriticalityEntity = A360.MasterDataManagement.Domain.Entities.Criticality;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface ICriticalityRepository : IMongoRepository<CriticalityEntity>
{
    Task<IReadOnlyCollection<CriticalityEntity>> SearchAsync(CriticalityFilter filter, CancellationToken cancellationToken = default);

    Task<CriticalityEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    // Unsets the default flag on every other criticality of the client.
    Task ClearDefaultAsync(string? clientId, string exceptCriticalityId, CancellationToken cancellationToken = default);
}

public sealed record CriticalityFilter(
    string? Search,
    string? Status,
    string? RiskLevel,
    string? MaintenancePriority,
    bool? DefaultCriticality,
    string? ClientId);
