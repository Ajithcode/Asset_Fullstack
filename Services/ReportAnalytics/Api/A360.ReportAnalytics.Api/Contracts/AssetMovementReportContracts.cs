namespace A360.ReportAnalytics.Api.Contracts;

/// <summary>
/// Query for GET /api/standard-reports/{reportId}/details on an Asset Movement report. Every value is optional; without dates
/// or times the report's saved period is used. Date picks one day (same as StartDate = EndDate = Date).
/// Dates are yyyy-MM-dd and times HH:mm or HH:mm:ss, both in TimeZone (default: the report's time zone).
/// </summary>
public sealed record AssetMovementReportRequest(
    DateOnly? Date = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    string? TimeZone = null,
    string? AssetId = null,
    string? Category = null,
    string? SubCategory = null)
{
    public bool NamesPeriod => Date is not null || StartDate is not null || EndDate is not null || StartTime is not null || EndTime is not null;
}

/// <summary>ToUtc is exclusive: rows are read with timestamp &gt;= FromUtc and &lt; ToUtc.</summary>
public sealed record AssetMovementReportResponse(
    DateTime? FromUtc,
    DateTime? ToUtc,
    string TimeZone,
    int TotalRecords,
    bool Truncated,
    IReadOnlyList<AssetMovementDateGroup> Dates);

/// <summary>The zone visits that start on one calendar date (in the report's time zone), oldest first.</summary>
public sealed record AssetMovementDateGroup(
    string Date,
    int RecordCount,
    IReadOnlyList<AssetMovementRow> Records);

/// <summary>
/// One stay of an asset in a zone. ZoneIn / ZoneOut are UTC; ZoneInLocal / ZoneOutLocal are in the report's time zone
/// (yyyy-MM-dd HH:mm:ss). TimeSpent is HH:mm:ss (hours may pass 24). ZoneName is null when the gateway is not plotted in a zone.
/// </summary>
/// <param name="AssetId">The asset registration id (e.g. AR000001).</param>
/// <param name="TagId">The asset's tags heard in the zone, comma separated.</param>
/// <param name="DeviceId">The gateways that heard it there, comma separated.</param>
public sealed record AssetMovementRow(
    string AssetId,
    string? AssetName,
    string? Category,
    string? SubCategory,
    string TagId,
    string? ZoneName,
    string DeviceId,
    DateTime ZoneIn,
    DateTime ZoneOut,
    string ZoneInLocal,
    string ZoneOutLocal,
    long TimeSpentSeconds,
    string TimeSpent,
    int ReadingCount);
