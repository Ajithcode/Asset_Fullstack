using ConditionEntity = A360.MasterDataManagement.Domain.Entities.Condition;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IConditionRepository : IMongoRepository<ConditionEntity>
{
    Task<IReadOnlyCollection<ConditionEntity>> SearchAsync(ConditionFilter filter, CancellationToken cancellationToken = default);

    Task<ConditionEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    // Unsets the default flag on every other condition of the client.
    Task ClearDefaultAsync(string? clientId, string exceptConditionId, CancellationToken cancellationToken = default);
}

public sealed record ConditionFilter(
    string? Search,
    string? Status,
    string? Type,
    bool? DefaultCondition,
    string? ClientId);
