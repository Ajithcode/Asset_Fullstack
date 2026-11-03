using WarehouseEntity = A360.MasterDataManagement.Domain.Entities.Warehouse;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IWarehouseRepository : IMongoRepository<WarehouseEntity>
{
    Task<IReadOnlyCollection<WarehouseEntity>> SearchAsync(WarehouseFilter filter, CancellationToken cancellationToken = default);

    Task<WarehouseEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithManagerAsync(string managerId, CancellationToken cancellationToken = default);

    Task UpdateManagerNameAsync(string managerId, string managerName, CancellationToken cancellationToken = default);
}

public sealed record WarehouseFilter(
    string? Search,
    string? Status,
    string? ManagerId,
    string? ClientId);
