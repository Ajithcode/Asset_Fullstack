using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetCustomFieldRepository : IMongoRepository<AssetCustomFieldDefinition>
{
    Task<IReadOnlyCollection<AssetCustomFieldDefinition>> GetByScopesAsync(IReadOnlyCollection<string> scopes, CancellationToken cancellationToken = default);

    /// <summary>Puts the given fields in the group in the given order, and takes every other field out of it.</summary>
    Task SetGroupFieldsAsync(string groupId, IReadOnlyList<string> orderedFieldIds, CancellationToken cancellationToken = default);

    Task ClearGroupAsync(string groupId, CancellationToken cancellationToken = default);

    Task<int> NextDisplayOrderAsync(string groupId, CancellationToken cancellationToken = default);

    Task<bool> AnyAppliesToCategoryAsync(string categoryId, CancellationToken cancellationToken = default);
}
