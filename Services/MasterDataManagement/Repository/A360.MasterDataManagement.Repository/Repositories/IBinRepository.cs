using BinEntity = A360.MasterDataManagement.Domain.Entities.Bin;
using A360.MasterDataManagement.Domain.ValueObjects;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IBinRepository : IMongoRepository<BinEntity>
{
    Task<IReadOnlyCollection<BinEntity>> SearchAsync(BinFilter filter, CancellationToken cancellationToken = default);

    // Codes only need to be unique inside their store.
    Task<BinEntity?> GetByCodeAsync(string code, string storeId, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithStoreAsync(string storeId, CancellationToken cancellationToken = default);

    Task UpdateStoreNameAsync(string storeId, string storeName, CancellationToken cancellationToken = default);

    Task<bool> AnyWithWarehouseAsync(string warehouseId, CancellationToken cancellationToken = default);

    Task UpdateWarehouseNameAsync(string warehouseId, string warehouseName, CancellationToken cancellationToken = default);

    // Keeps records under a store that moved to another warehouse pointing at the new one.
    Task SetWarehouseForStoreAsync(string storeId, MasterDataReference? warehouse, CancellationToken cancellationToken = default);
}

public sealed record BinFilter(
    string? Search,
    string? Status,
    string? StoreId,
    string? WarehouseId,
    string? ClientId);
