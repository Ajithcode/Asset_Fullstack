
using AssetLinkingEntity = A360.Wip.Domain.Entities.AssetLinking;
using A360.Repository.Repositories;

namespace A360.Wip.Repository.Repositories;

public interface IAssetLinkingRepository : IMongoRepository<AssetLinkingEntity>
{
    Task<AssetLinkingEntity?> GetByAssetIdAsync(string assetId, CancellationToken cancellationToken = default);
}
