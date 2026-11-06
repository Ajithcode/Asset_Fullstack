using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetDedupDismissalRepository : IMongoRepository<AssetDedupDismissal>
{
    /// <summary>Every active (not deleted) dismissal, newest first.</summary>
    Task<IReadOnlyCollection<AssetDedupDismissal>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<AssetDedupDismissal?> GetByPairKeyAsync(string pairKey, CancellationToken cancellationToken = default);
}
