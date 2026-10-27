using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetModelRepository : IMongoRepository<AssetModel>
{
    /// <summary>Model codes are unique across all models, ignoring case.</summary>
    Task<bool> CodeExistsAsync(string modelCode, string? excludeId, CancellationToken cancellationToken = default);

    /// <summary>Model names are unique within an asset type, ignoring case.</summary>
    Task<bool> NameExistsInTypeAsync(string modelName, string assetTypeId, string? excludeId, CancellationToken cancellationToken = default);

    Task<bool> AnyInTypeAsync(string assetTypeId, CancellationToken cancellationToken = default);

    /// <summary>Number of models per asset type id.</summary>
    Task<IReadOnlyDictionary<string, long>> CountByTypeAsync(CancellationToken cancellationToken = default);
}
