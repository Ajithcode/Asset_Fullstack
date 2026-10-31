using StandardEntity = A360.MasterDataManagement.Domain.Entities.Standard;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IStandardRepository : IMongoRepository<StandardEntity>
{
    Task<IReadOnlyCollection<StandardEntity>> SearchAsync(StandardFilter filter, CancellationToken cancellationToken = default);

    Task<StandardEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);
}

public sealed record StandardFilter(
    string? Search,
    string? Status,
    string? Organization,
    string? Category,
    string? ClientId);
