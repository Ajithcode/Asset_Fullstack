using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetRegistrationRepository : IMongoRepository<AssetRegistration>
{
    Task<bool> AssetTagExistsAsync(string assetTag, CancellationToken cancellationToken = default);

    Task<AssetRegistration?> GetByAssetRegistrationIdAsync(string assetRegistrationId, CancellationToken cancellationToken = default);

    /// <summary>Assets whose tag or serial number is one of <paramref name="tagsOrSerials"/> (exact match).</summary>
    Task<IReadOnlyCollection<AssetRegistration>> FindByTagsOrSerialsAsync(IReadOnlyCollection<string> tagsOrSerials, CancellationToken cancellationToken = default);

    /// <summary>Every asset matching an audit scope, sorted by tag; at most <paramref name="limit"/>.</summary>
    Task<IReadOnlyCollection<AssetRegistration>> FindInScopeAsync(AssetScopeQuery query, int limit, CancellationToken cancellationToken = default);

    Task<AssetRegistrationQueryResult> QueryAsync(AssetRegistrationQuery query, CancellationToken cancellationToken = default);
}

/// <summary>An audit scope. Empty values mean "all"; names are matched exactly, as in the asset list filters.</summary>
/// <param name="LocationId">Any level of the location hierarchy; assets at or below it match.</param>
/// <param name="TagFrom">With TagTo, asset tags in this range (compared as text, so keep the same padding).</param>
/// <param name="AssetRegistrationIds">When set, only these assets.</param>
public sealed record AssetScopeQuery(
    string? LocationId,
    string? Department,
    string? Category,
    string? TagFrom,
    string? TagTo,
    string? Status,
    string? AssetType,
    string? Criticality,
    IReadOnlyCollection<string>? AssetRegistrationIds = null);
