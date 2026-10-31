using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetCategoryTypeRepository : IMongoRepository<AssetCategoryType>
{
    /// <summary>Type codes are unique across all types, ignoring case.</summary>
    Task<bool> CodeExistsAsync(string typeCode, string? excludeId, CancellationToken cancellationToken = default);

    /// <summary>Type names are unique within a category, ignoring case.</summary>
    Task<bool> NameExistsInCategoryAsync(string typeName, string categoryId, string? excludeId, CancellationToken cancellationToken = default);

    Task<bool> AnyInCategoryAsync(string categoryId, CancellationToken cancellationToken = default);
}
