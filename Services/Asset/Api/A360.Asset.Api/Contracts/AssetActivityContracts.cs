using A360.Asset.Repository.Activity;

namespace A360.Asset.Api.Contracts;

/// <summary>
/// One row of the Recent Activity table. Show <see cref="At"/> with its time only when <see cref="HasTime"/> is true.
/// </summary>
public sealed record AssetActivityItemResponse(
    DateTime At,
    bool HasTime,
    string Type,
    string Title,
    string? Description,
    string? By,
    string? Reference,
    string? SourceId)
{
    public static AssetActivityItemResponse From(AssetActivityItem item) =>
        new(item.At, item.HasTime, item.Type, item.Title, item.Description, item.By, item.Reference, item.SourceId);
}

/// <param name="TypeCounts">Rows per type within the date range, for filter chips; only types that have rows.</param>
/// <param name="Types">Every activity type, for a type filter dropdown.</param>
public sealed record AssetActivityResponse(
    string AssetRegistrationId,
    IReadOnlyList<AssetActivityItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyDictionary<string, int> TypeCounts,
    IReadOnlyList<string> Types)
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 200;

    public static AssetActivityResponse From(
        string assetRegistrationId,
        IReadOnlyList<AssetActivityItem> activity,
        string? type,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize;

        // A date without a time ("2026-10-31") includes that whole day.
        var until = to is { TimeOfDay.Ticks: 0 } ? to.Value.AddDays(1) : to;

        var inRange = activity
            .Where(item => from is null || item.At >= from.Value)
            .Where(item => until is null || item.At < until.Value)
            .ToList();

        var typeCounts = inRange
            .GroupBy(item => item.Type)
            .ToDictionary(group => group.Key, group => group.Count());

        var filtered = string.IsNullOrWhiteSpace(type)
            ? inRange
            : inRange.Where(item => string.Equals(item.Type, type.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        return new AssetActivityResponse(
            assetRegistrationId,
            filtered.Skip((page - 1) * pageSize).Take(pageSize).Select(AssetActivityItemResponse.From).ToList(),
            filtered.Count,
            page,
            pageSize,
            typeCounts,
            AssetActivityTypes.All);
    }
}
