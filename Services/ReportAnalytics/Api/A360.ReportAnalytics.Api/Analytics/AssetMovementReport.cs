using A360.ReportAnalytics.Api.Contracts;
using A360.ReportAnalytics.Domain.Entities;
using A360.ReportAnalytics.Repository.Repositories;

namespace A360.ReportAnalytics.Api.Analytics;

/// <summary>
/// Rows of an Asset Movement standard report: every tag reading in NetworkTimeSeries for the period, with the registered asset
/// that carries the tag (AssetRegistration.basic_information.asset_tag) and the zone of the gateway that heard it
/// (maps_plots.device_id), grouped by date in the report's time zone.
/// </summary>
public static class AssetMovementReport
{
    private const int MaxRecords = 50_000;

    public sealed record Result(AssetMovementReportResponse? Data, Dictionary<string, string[]>? Errors);

    /// <summary>
    /// Uses the report's saved period unless the request names dates or times. Requested periods run from
    /// startDate + startTime (default 00:00) to endDate + endTime (default end of day), both inclusive; date sets both days,
    /// and a time without its date applies to the other date, or to today.
    /// </summary>
    public static async Task<Result> BuildAsync(
        StandardReport report,
        AssetMovementReportRequest request,
        IAssetMovementReportRepository repository,
        string? tenantId,
        string? clientId,
        CancellationToken cancellationToken)
    {
        var timeZoneId = string.IsNullOrWhiteSpace(request.TimeZone) ? report.TimeZone : request.TimeZone.Trim();
        if (!ReportPeriods.TryGetTimeZone(timeZoneId, out var timeZone))
        {
            return Problem("TimeZone", $"Unknown time zone '{timeZoneId}'");
        }

        var (fromUtc, toUtcExclusive) = request.NamesPeriod
            ? RequestedPeriod(request, timeZone)
            : SavedPeriod(report);

        if (fromUtc is not null && toUtcExclusive is not null && fromUtc >= toUtcExclusive)
        {
            return Problem("EndDate", "The end of the period must be after its start");
        }

        var result = await repository.GetAsync(
            new AssetMovementQuery(
                fromUtc,
                toUtcExclusive,
                tenantId,
                clientId,
                request.AssetId,
                request.Category,
                request.SubCategory,
                MaxRecords),
            cancellationToken);

        var dates = result.Records
            .Select(record => (Record: record, Local: TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(record.Timestamp, DateTimeKind.Utc), timeZone)))
            .GroupBy(item => item.Local.Date)
            .OrderBy(day => day.Key)
            .Select(day =>
            {
                var rows = day.Select(item => new AssetMovementRow(
                        item.Record.Timestamp,
                        item.Local.ToString("HH:mm:ss"),
                        item.Record.TagId,
                        item.Record.AssetMongoId,
                        item.Record.AssetRegistrationId,
                        item.Record.AssetName,
                        item.Record.Category,
                        item.Record.SubCategory,
                        item.Record.DeviceId,
                        item.Record.ZoneName,
                        item.Record.ZoneCode))
                    .ToList();
                return new AssetMovementDateGroup(day.Key.ToString("yyyy-MM-dd"), rows.Count, rows);
            })
            .ToList();

        return new Result(
            new AssetMovementReportResponse(fromUtc, toUtcExclusive, timeZoneId, result.Records.Count, result.Truncated, dates),
            null);
    }

    private static (DateTime? From, DateTime? ToExclusive) RequestedPeriod(AssetMovementReportRequest request, TimeZoneInfo timeZone)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var requestStart = request.StartDate ?? request.Date;
        var requestEnd = request.EndDate ?? request.Date;
        var startDate = requestStart ?? (request.StartTime is null ? null : requestEnd ?? today);
        var endDate = requestEnd ?? (request.EndTime is null ? null : requestStart ?? today);

        DateTime? from = startDate is null
            ? null
            : ToUtc(startDate.Value.ToDateTime(request.StartTime ?? TimeOnly.MinValue), timeZone);

        DateTime? toExclusive = endDate is null
            ? null
            : request.EndTime is { } endTime
                ? ThroughEndOf(ToUtc(endDate.Value.ToDateTime(endTime), timeZone))
                : ToUtc(endDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), timeZone);

        return (from, toExclusive);
    }

    /// <summary>The saved period is stored in UTC; whole-day periods end on the last millisecond of the day.</summary>
    private static (DateTime? From, DateTime? ToExclusive) SavedPeriod(StandardReport report)
    {
        DateTime? from = report.ReportPeriodFrom is null ? null : DateTime.SpecifyKind(report.ReportPeriodFrom.Value, DateTimeKind.Utc);
        DateTime? to = report.ReportPeriodTo is null ? null : DateTime.SpecifyKind(report.ReportPeriodTo.Value, DateTimeKind.Utc);
        return (from, to is null ? null : report.IncludeTime ? ThroughEndOf(to.Value) : to.Value.AddMilliseconds(1));
    }

    // An end time is inclusive: 18:00 covers up to 18:00:59.999, 18:00:30 up to 18:00:30.999.
    private static DateTime ThroughEndOf(DateTime value) =>
        value.Millisecond != 0 ? value.AddMilliseconds(1)
        : value.Second == 0 ? value.AddMinutes(1)
        : value.AddSeconds(1);

    private static DateTime ToUtc(DateTime local, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), timeZone);

    private static Result Problem(string field, string message) =>
        new(null, new Dictionary<string, string[]> { [field] = [message] });
}
