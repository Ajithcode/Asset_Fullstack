using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetCategoryRepository : IMongoRepository<AssetCategory>
{
    /// <summary>Codes are unique across all categories, ignoring case.</summary>
    Task<bool> CodeExistsAsync(string code, string? excludeId, CancellationToken cancellationToken = default);

    /// <summary>Names are unique among categories sharing the same parent, ignoring case.</summary>
    Task<bool> NameExistsUnderParentAsync(string name, string? parentCategoryId, string? excludeId, CancellationToken cancellationToken = default);

    Task<bool> HasChildrenAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Saves the category's own details, leaving its custom fields and attributes as they are.</summary>
    Task<bool> UpdateDetailsAsync(AssetCategory category, CancellationToken cancellationToken = default);

    Task<bool> AddCustomFieldAsync(string categoryId, AssetCategoryCustomField field, CancellationToken cancellationToken = default);

    Task<bool> RemoveCustomFieldAsync(string categoryId, string fieldId, CancellationToken cancellationToken = default);

    Task<bool> AddAttributeAsync(string categoryId, AssetCategoryAttribute attribute, CancellationToken cancellationToken = default);

    Task<bool> RemoveAttributeAsync(string categoryId, string attributeId, CancellationToken cancellationToken = default);
}
