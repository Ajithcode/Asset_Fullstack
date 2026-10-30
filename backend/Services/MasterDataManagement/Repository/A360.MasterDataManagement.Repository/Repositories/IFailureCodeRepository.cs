using FailureCodeEntity = A360.MasterDataManagement.Domain.Entities.FailureCode;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IFailureCodeRepository : IMongoRepository<FailureCodeEntity>
{
    Task<IReadOnlyCollection<FailureCodeEntity>> SearchAsync(FailureCodeFilter filter, CancellationToken cancellationToken = default);

    Task<FailureCodeEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);
}

public sealed record FailureCodeFilter(
    string? Search,
    string? Status,
    string? Category,
    string? Severity,
    string? ClientId);
