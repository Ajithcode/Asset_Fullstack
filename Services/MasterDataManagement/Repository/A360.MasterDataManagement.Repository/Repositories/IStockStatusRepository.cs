using StockStatusEntity = A360.MasterDataManagement.Domain.Entities.StockStatus;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IStockStatusRepository : IMongoRepository<StockStatusEntity>
{
    Task<IReadOnlyCollection<StockStatusEntity>> SearchAsync(StockStatusFilter filter, CancellationToken cancellationToken = default);

    Task<StockStatusEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    // Unsets SystemDefault on every other record of the client.
    Task ClearSystemDefaultAsync(string? clientId, string exceptId, CancellationToken cancellationToken = default);
}

public sealed record StockStatusFilter(
    string? Search,
    string? Status,
    string? Type,
    bool? SystemDefault,
    string? ClientId);
