using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetCriticalityAssessmentRepository : IMongoRepository<AssetCriticalityAssessment>
{
    /// <summary>Asset Criticality List: the current assessment of each asset.</summary>
    Task<AssetCriticalityQueryResult> QueryCurrentAsync(AssetCriticalityQuery query, CancellationToken cancellationToken = default);

    Task<AssetCriticalityAssessment?> GetCurrentAsync(string assetRegistrationId, CancellationToken cancellationToken = default);

    /// <summary>Review History: every assessment of the asset, latest first.</summary>
    Task<IReadOnlyCollection<AssetCriticalityAssessment>> GetHistoryAsync(string assetRegistrationId, CancellationToken cancellationToken = default);

    /// <summary>Clears IsCurrent on the asset's assessments, before a new current one is saved.</summary>
    Task ClearCurrentAsync(string assetRegistrationId, CancellationToken cancellationToken = default);
}

/// <summary>Filters and paging for the Asset Criticality List. Empty values mean "all".</summary>
public sealed record AssetCriticalityQuery(
    string? Search,
    string? Category,
    string? Location,
    string? CriticalityLevel,
    int Page,
    int PageSize);

public sealed record AssetCriticalityQueryResult(
    IReadOnlyCollection<AssetCriticalityAssessment> Items,
    long TotalCount);
