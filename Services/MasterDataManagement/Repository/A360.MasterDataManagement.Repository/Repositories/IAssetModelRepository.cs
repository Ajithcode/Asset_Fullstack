using AssetModelEntity = A360.MasterDataManagement.Domain.Entities.AssetModel;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Repository.Repositories;

public interface IAssetModelRepository : IMongoRepository<AssetModelEntity>
{
    Task<IReadOnlyCollection<AssetModelEntity>> SearchAsync(AssetModelFilter filter, CancellationToken cancellationToken = default);

    Task<AssetModelEntity?> GetByModelCodeAsync(string modelCode, string? clientId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithAssetTypeAsync(string assetTypeId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithCategoryAsync(string categoryId, CancellationToken cancellationToken = default);

    Task<bool> AnyWithManufacturerAsync(string manufacturerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AssetModelEntity>> GetByManufacturersAsync(IEnumerable<string> manufacturerIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AssetModelEntity>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);

    Task UpdateManufacturerNameAsync(string manufacturerId, string manufacturerName, CancellationToken cancellationToken = default);

    // Makes the manufacturer's models exactly the given ones: listed models are pointed at it, others it had are cleared.
    Task SetManufacturerModelsAsync(string manufacturerId, string manufacturerName, IReadOnlyCollection<string> modelIds, CancellationToken cancellationToken = default);

    Task UpdateAssetTypeNameAsync(string assetTypeId, string assetTypeName, CancellationToken cancellationToken = default);

    Task UpdateCategoryNameAsync(string categoryId, string categoryName, CancellationToken cancellationToken = default);
}

public sealed record AssetModelFilter(
    string? Search,
    string? Status,
    string? ManufacturerId,
    string? AssetTypeId,
    string? CategoryId,
    string? Lifecycle,
    string? ClientId);
