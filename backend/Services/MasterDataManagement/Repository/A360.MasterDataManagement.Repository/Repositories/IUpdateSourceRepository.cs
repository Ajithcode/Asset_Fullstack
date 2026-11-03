using UpdateSourceEntity = A360.MasterDataManagement.Domain.Entities.UpdateSource;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IUpdateSourceRepository : IMongoRepository<UpdateSourceEntity>
{
    Task<IReadOnlyCollection<UpdateSourceEntity>> SearchAsync(UpdateSourceFilter filter, CancellationToken cancellationToken = default);

    Task<UpdateSourceEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);
}

public sealed record UpdateSourceFilter(
    string? Search,
    string? Status,
    string? Type,
    string? ClientId);
