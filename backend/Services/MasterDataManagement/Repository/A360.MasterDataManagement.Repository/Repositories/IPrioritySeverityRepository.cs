using PrioritySeverityEntity = A360.MasterDataManagement.Domain.Entities.PrioritySeverity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IPrioritySeverityRepository : IMongoRepository<PrioritySeverityEntity>
{
    Task<IReadOnlyCollection<PrioritySeverityEntity>> SearchAsync(PrioritySeverityFilter filter, CancellationToken cancellationToken = default);

    Task<PrioritySeverityEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);
}

public sealed record PrioritySeverityFilter(
    string? Search,
    string? Status,
    string? Type,
    string? ClientId);
