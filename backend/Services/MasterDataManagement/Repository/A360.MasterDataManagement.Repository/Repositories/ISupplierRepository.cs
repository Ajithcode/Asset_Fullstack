using SupplierEntity = A360.MasterDataManagement.Domain.Entities.Supplier;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface ISupplierRepository : IMongoRepository<SupplierEntity>
{
    Task<IReadOnlyCollection<SupplierEntity>> SearchAsync(SupplierFilter filter, CancellationToken cancellationToken = default);

    Task<SupplierEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);
}

public sealed record SupplierFilter(
    string? Search,
    string? Status,
    string? Country,
    string? Category,
    string? ClientId);
