using A360.Asset.Domain.Entities;
using A360.Repository.Repositories;

namespace A360.Asset.Repository.Repositories;

public interface IAssetServiceRequestRepository : IMongoRepository<AssetServiceRequest>
{
    Task<AssetServiceRequestQueryResult> QueryAsync(AssetServiceRequestQuery query, CancellationToken cancellationToken = default);

    Task<AssetServiceRequest?> GetByRequestNumberAsync(string requestNumber, CancellationToken cancellationToken = default);
}

/// <summary>Filters and paging for the service request list. Empty values mean "all".</summary>
/// <param name="AssetRegistrationId">One asset's requests (the asset page and Recent Service Requests).</param>
/// <param name="FromDate">Requested on or after this date.</param>
/// <param name="ToDate">Requested on or before this date.</param>
public sealed record AssetServiceRequestQuery(
    string? Search,
    string? AssetRegistrationId,
    string? Status,
    string? Priority,
    string? RequestType,
    string? AssignedToUserId,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page,
    int PageSize);

/// <param name="StatusCounts">Request counts per status for the same filters except Status.</param>
public sealed record AssetServiceRequestQueryResult(
    IReadOnlyCollection<AssetServiceRequest> Items,
    long TotalCount,
    IReadOnlyDictionary<string, long> StatusCounts);
