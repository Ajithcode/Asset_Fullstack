using ItemEntity = A360.MasterDataManagement.Domain.Entities.Item;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IItemRepository : IMongoRepository<ItemEntity>
{
    Task<IReadOnlyCollection<ItemEntity>> SearchAsync(ItemFilter filter, CancellationToken cancellationToken = default);

    Task<ItemEntity?> GetByItemCodeAsync(string itemCode, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithCategoryAsync(string categoryId, CancellationToken cancellationToken = default);

    Task UpdateCategoryNameAsync(string categoryId, string categoryName, CancellationToken cancellationToken = default);

    Task<bool> AnyWithUomAsync(string uomId, CancellationToken cancellationToken = default);

    Task UpdateUomNameAsync(string uomId, string uomName, CancellationToken cancellationToken = default);
}

public sealed record ItemFilter(
    string? Search,
    string? Status,
    string? CategoryId,
    string? UomId,
    string? ClientId);
