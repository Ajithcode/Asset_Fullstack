using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetAssignmentRepository : IMongoRepository<AssetAssignmentRecord>
{
    Task<AssetAssignmentQueryResult> QueryAsync(AssetAssignmentQuery query, CancellationToken cancellationToken = default);

    /// <summary>Of the given assets, the ones already in an active assignment that has not ended before <paramref name="asOf"/>.</summary>
    /// <param name="excludeAssignmentId">Leaves out this assignment, so an assignment being edited does not conflict with itself.</param>
    Task<IReadOnlyCollection<string>> GetAssignedAssetIdsAsync(
        IReadOnlyCollection<string> assetRegistrationIds,
        DateTime asOf,
        string? excludeAssignmentId = null,
        CancellationToken cancellationToken = default);

    /// <summary>The active assignments, not ended before <paramref name="asOf"/>, that include any of the given assets.</summary>
    Task<IReadOnlyCollection<AssetAssignmentRecord>> GetActiveByAssetIdsAsync(
        IReadOnlyCollection<string> assetRegistrationIds,
        DateTime asOf,
        CancellationToken cancellationToken = default);
}

/// <summary>Filters and paging for the assignment list. Empty values mean "all".</summary>
/// <param name="AssetRegistrationId">Assignments that include this asset.</param>
/// <param name="FromDate">Assignments still running on or after this date.</param>
/// <param name="ToDate">Assignments starting on or before this date.</param>
public sealed record AssetAssignmentQuery(
    string? Search,
    string? AssignTo,
    string? Status,
    string? AssetRegistrationId,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page,
    int PageSize);

/// <param name="StatusCounts">Assignment counts per status for the same filters except Status.</param>
public sealed record AssetAssignmentQueryResult(
    IReadOnlyCollection<AssetAssignmentRecord> Items,
    long TotalCount,
    IReadOnlyDictionary<string, long> StatusCounts);
