using StoreEntity = A360.MasterDataManagement.Domain.Entities.Store;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IStoreRepository : IMongoRepository<StoreEntity>
{
    Task<IReadOnlyCollection<StoreEntity>> SearchAsync(StoreFilter filter, CancellationToken cancellationToken = default);

    // Codes only need to be unique inside their warehouse.
    Task<StoreEntity?> GetByCodeAsync(string code, string warehouseId, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithWarehouseAsync(string warehouseId, CancellationToken cancellationToken = default);

    Task UpdateWarehouseNameAsync(string warehouseId, string warehouseName, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> CountByWarehouseAsync(IEnumerable<string> warehouseIds, CancellationToken cancellationToken = default);
}

public sealed record StoreFilter(
    string? Search,
    string? Status,
    string? WarehouseId,
    string? ClientId);
