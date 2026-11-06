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

    /// <summary>
    /// Every registered asset with only the fields the asset list filters on (category, type, status, location,
    /// department, custodian, criticality, manufacturer, condition), for building the filter dropdowns.
    /// </summary>
    Task<IReadOnlyCollection<AssetRegistration>> GetFilterFieldsAsync(CancellationToken cancellationToken = default);

    /// <summary>Assets whose AssetRegistrationId is one of <paramref name="assetRegistrationIds"/> or whose tag is one of <paramref name="assetTags"/> (exact match).</summary>
    Task<IReadOnlyCollection<AssetRegistration>> FindByIdsOrTagsAsync(
        IReadOnlyCollection<string> assetRegistrationIds,
        IReadOnlyCollection<string> assetTags,
        CancellationToken cancellationToken = default);

    /// <summary>Assets matching the export filters, sorted by tag; at most <paramref name="limit"/>.</summary>
    Task<IReadOnlyCollection<AssetRegistration>> FindForExportAsync(AssetExportQuery query, int limit, CancellationToken cancellationToken = default);

    Task<long> CountForExportAsync(AssetExportQuery query, CancellationToken cancellationToken = default);
}

/// <summary>Filters of the Export Assets panel. Empty values mean "all"; names are matched exactly, as in the asset list filters.</summary>
/// <param name="LocationId">Any level of the location hierarchy; assets at or below it match.</param>
/// <param name="NextMaintenanceFrom">With NextMaintenanceTo, the Date Range filter on the next maintenance date (inclusive).</param>
public sealed record AssetExportQuery(
    string? Search,
    string? Category,
    string? AssetType,
    string? Status,
    string? LocationId,
    string? Department,
    string? Criticality,
    string? Condition,
    string? Manufacturer,
    DateTime? NextMaintenanceFrom,
    DateTime? NextMaintenanceTo);

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
