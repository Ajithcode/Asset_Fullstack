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

    /// <summary>The asset's Draft / Submitted / PendingApproval / Rejected assessment, if any.</summary>
    Task<AssetCriticalityAssessment?> GetOpenAsync(string assetRegistrationId, CancellationToken cancellationToken = default);

    /// <summary>Every assessment of the asset (any status except cancelled), newest first: the version history.</summary>
    Task<IReadOnlyCollection<AssetCriticalityAssessment>> GetVersionsAsync(string assetRegistrationId, CancellationToken cancellationToken = default);

    /// <summary>Next ACA-yyyy-nnnnnn number.</summary>
    Task<string> NextNumberAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves an edit only when the stored version and status still match; false when someone changed it first.</summary>
    Task<bool> ReplaceIfAsync(AssetCriticalityAssessment assessment, int expectedVersion, IReadOnlyCollection<string> allowedStatuses, CancellationToken cancellationToken = default);

    /// <summary>Atomic state change: applies <paramref name="update"/> (and bumps the version) only if the assessment is in one of <paramref name="fromStatuses"/>. Null when it is not.</summary>
    Task<AssetCriticalityAssessment?> TransitionAsync(string id, IReadOnlyCollection<string> fromStatuses, int? expectedVersion, MongoDB.Driver.UpdateDefinition<AssetCriticalityAssessment> update, CancellationToken cancellationToken = default);

    /// <summary>Clears IsCurrent on every assessment of the asset except <paramref name="keepId"/>.</summary>
    Task ClearCurrentExceptAsync(string assetRegistrationId, string keepId, CancellationToken cancellationToken = default);
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
