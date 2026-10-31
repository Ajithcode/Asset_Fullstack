using AssetTypeEntity = A360.MasterDataManagement.Domain.Entities.AssetType;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IAssetTypeRepository : IMongoRepository<AssetTypeEntity>
{
    Task<IReadOnlyCollection<AssetTypeEntity>> SearchAsync(AssetTypeFilter filter, CancellationToken cancellationToken = default);

    Task<AssetTypeEntity?> GetByCodeAsync(string code, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithCategoryAsync(string categoryId, CancellationToken cancellationToken = default);

    Task UpdateCategoryNameAsync(string categoryId, string categoryName, CancellationToken cancellationToken = default);
}

public sealed record AssetTypeFilter(
    string? Search,
    string? Status,
    string? CategoryId,
    string? AssetClass,
    string? DepreciationMethod,
    string? ClientId);
