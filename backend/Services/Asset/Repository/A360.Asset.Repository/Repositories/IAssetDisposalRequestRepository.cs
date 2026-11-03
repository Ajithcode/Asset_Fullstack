using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetDisposalRequestRepository : IMongoRepository<AssetDisposalRequest>
{
    Task<AssetDisposalRequestQueryResult> QueryAsync(AssetDisposalRequestQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Of the given assets, those already in a submitted request that is still open (Pending Approval or Approved),
    /// mapped to that request's number.
    /// </summary>
    /// <param name="excludeId">Leaves out this request, so a request does not conflict with itself.</param>
    Task<IReadOnlyDictionary<string, string>> GetOpenRequestNumbersAsync(
        IReadOnlyCollection<string> assetRegistrationIds,
        string? excludeId,
        CancellationToken cancellationToken = default);
}

/// <summary>Filters and paging for the disposal request list. Empty values mean "all".</summary>
/// <param name="ApproverUserId">Requests waiting on this user's approval ("My approvals").</param>
public sealed record AssetDisposalRequestQuery(
    string? Search,
    string? Status,
    string? Method,
    string? Reason,
    string? ApproverUserId,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page,
    int PageSize);

/// <param name="StatusCounts">Request counts per status for the same filters except Status.</param>
public sealed record AssetDisposalRequestQueryResult(
    IReadOnlyCollection<AssetDisposalRequest> Items,
    long TotalCount,
    IReadOnlyDictionary<string, long> StatusCounts);
