using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetDedupRulesRepository : IMongoRepository<AssetDedupRules>
{
    /// <summary>The saved rules, or null before they are first saved.</summary>
    Task<AssetDedupRules?> GetCurrentAsync(CancellationToken cancellationToken = default);
}
