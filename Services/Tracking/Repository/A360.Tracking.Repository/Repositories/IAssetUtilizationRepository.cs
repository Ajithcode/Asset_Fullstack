using A360.Tracking.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Tracking.Repository.Repositories;

public interface IAssetUtilizationRepository : IMongoRepository<AssetUtilization>
{
    Task<AssetUtilization?> GetByUtilizationIdAsync(string utilizationId, CancellationToken cancellationToken = default);

    /// <summary>The record of an asset for a day, used to keep one record per asset per day.</summary>
    Task<AssetUtilization?> GetByAssetAndDateAsync(string assetRegistrationId, DateTime dateUtc, CancellationToken cancellationToken = default);

    /// <summary>Records with a date in [from, to), newest day first; optionally only one asset type.</summary>
    Task<IReadOnlyCollection<AssetUtilization>> GetInRangeAsync(DateTime fromUtc, DateTime toUtc, string? assetType = null, CancellationToken cancellationToken = default);
}
