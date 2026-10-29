using UomEntity = A360.MasterDataManagement.Domain.Entities.Uom;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IUomRepository : IMongoRepository<UomEntity>
{
    Task<IReadOnlyCollection<UomEntity>> SearchAsync(UomFilter filter, CancellationToken cancellationToken = default);

    Task<UomEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);
}

public sealed record UomFilter(
    string? Search,
    string? Status,
    string? Type,
    string? ClientId);
