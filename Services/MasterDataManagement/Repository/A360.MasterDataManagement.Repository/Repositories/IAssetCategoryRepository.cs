using AssetCategoryEntity = A360.MasterDataManagement.Domain.Entities.AssetCategory;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IAssetCategoryRepository : IMongoRepository<AssetCategoryEntity>
{
    Task<IReadOnlyCollection<AssetCategoryEntity>> SearchAsync(AssetCategoryFilter filter, CancellationToken cancellationToken = default);

    Task<AssetCategoryEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    // First of `codes` already used as a subcategory code by another category of the client, or null.
    Task<string?> FindSubcategoryCodeInUseAsync(IReadOnlyCollection<string> codes, string? clientId, string? exceptCategoryId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AssetCategoryEntity>> GetChildrenAsync(string parentCategoryId, CancellationToken cancellationToken = default);

    Task<bool> HasChildrenAsync(string parentCategoryId, CancellationToken cancellationToken = default);

    Task UpdateParentCategoryNameAsync(string parentCategoryId, string parentCategoryName, CancellationToken cancellationToken = default);
}

public sealed record AssetCategoryFilter(
    string? Search,
    string? Status,
    string? AssetType,
    string? ParentCategoryId,
    string? Asset,
    string? ClientId);
