using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public sealed record AssetMergeQueryResult(IReadOnlyCollection<AssetMerge> Items, long TotalCount);

public interface IAssetMergeRepository : IMongoRepository<AssetMerge>
{
    /// <summary>Merge log, newest first; search matches the primary or a merged tag.</summary>
    Task<AssetMergeQueryResult> QueryAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);
}
