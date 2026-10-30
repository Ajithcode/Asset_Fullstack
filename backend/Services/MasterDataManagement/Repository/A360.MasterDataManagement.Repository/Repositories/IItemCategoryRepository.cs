using ItemCategoryEntity = A360.MasterDataManagement.Domain.Entities.ItemCategory;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IItemCategoryRepository : IMongoRepository<ItemCategoryEntity>
{
    Task<IReadOnlyCollection<ItemCategoryEntity>> SearchAsync(ItemCategoryFilter filter, CancellationToken cancellationToken = default);

    Task<ItemCategoryEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ItemCategoryEntity>> GetChildrenAsync(string parentId, CancellationToken cancellationToken = default);

    Task<bool> HasChildrenAsync(string parentId, CancellationToken cancellationToken = default);

    Task UpdateParentCategoryNameAsync(string parentId, string parentName, CancellationToken cancellationToken = default);
}

public sealed record ItemCategoryFilter(
    string? Search,
    string? Status,
    string? ParentCategoryId,
    string? ClientId);
