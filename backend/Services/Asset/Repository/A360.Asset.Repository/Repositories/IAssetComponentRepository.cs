using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetComponentRepository : IMongoRepository<AssetComponent>
{
    Task<IReadOnlyCollection<AssetComponent>> GetByAssetAsync(string assetRegistrationId, CancellationToken cancellationToken = default);

    /// <summary>Component tags are unique within an asset, ignoring case.</summary>
    Task<bool> TagExistsAsync(string assetRegistrationId, string componentTag, string? excludeId, CancellationToken cancellationToken = default);

    Task<bool> HasChildrenAsync(string componentId, CancellationToken cancellationToken = default);
}
