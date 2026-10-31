using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetHierarchyItemRepository : IMongoRepository<AssetHierarchyItem>
{
    /// <summary>Names are unique among items sharing the same parent, ignoring case.</summary>
    Task<bool> NameExistsUnderParentAsync(string name, string? parentId, string? excludeId, CancellationToken cancellationToken = default);

    /// <summary>An asset tag can appear on only one item, ignoring case.</summary>
    Task<bool> AssetTagExistsAsync(string assetTag, string? excludeId, CancellationToken cancellationToken = default);
}
