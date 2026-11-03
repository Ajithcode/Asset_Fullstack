using AssetSubCategoryEntity = A360.MasterDataManagement.Domain.Entities.AssetSubCategory;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IAssetSubCategoryRepository : IMongoRepository<AssetSubCategoryEntity>
{
    Task<IReadOnlyCollection<AssetSubCategoryEntity>> SearchAsync(AssetSubCategoryFilter filter, CancellationToken cancellationToken = default);

    Task<AssetSubCategoryEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithCategoryAsync(string categoryId, CancellationToken cancellationToken = default);

    Task UpdateCategoryNameAsync(string categoryId, string categoryName, CancellationToken cancellationToken = default);
}

public sealed record AssetSubCategoryFilter(
    string? Search,
    string? Status,
    string? AssetType,
    string? CategoryId,
    string? Asset,
    string? ClientId);
