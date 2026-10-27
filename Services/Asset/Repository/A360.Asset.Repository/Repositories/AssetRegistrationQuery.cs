using A360.Asset.Domain.Entities;

namespace A360.Asset.Repository.Repositories;

/// <summary>Filters, sorting and paging for the asset list. Empty values mean "all".</summary>
public sealed record AssetRegistrationQuery(
    string? Search,
    string? Category,
    string? AssetType,
    string? Status,
    string? LocationId,
    string? Department,
    string? CustodianUserId,
    string? Criticality,
    string? Manufacturer,
    string? SortBy,
    bool SortDescending,
    int Page,
    int PageSize);

/// <param name="StatusCounts">
/// Asset counts per status for the same filters except Status, so the summary cards
/// keep showing every status while one is selected.
/// </param>
public sealed record AssetRegistrationQueryResult(
    IReadOnlyCollection<AssetRegistration> Items,
    long TotalCount,
    IReadOnlyDictionary<string, long> StatusCounts);
