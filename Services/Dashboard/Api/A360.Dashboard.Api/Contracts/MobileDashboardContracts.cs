namespace A360.Dashboard.Api.Contracts;

// Responses of the mobile app dashboard (/api/dashboards/mobile/*). Every screen of the mobile Home & Dashboard
// design has one endpoint; the Home endpoint returns the summary cards and each "View all / View details" screen
// has its own endpoint with the full data.

/// <summary>One slice of a breakdown (donut / legend row): a stable key, the label to show, the count and its share (0–100).</summary>
public sealed record MobileBreakdownItem(string Key, string Label, int Count, decimal Percent);

/// <summary>A value with its change against the previous period (percent, null when there is nothing to compare).</summary>
public sealed record MobileMetricValue(decimal Value, string Unit, decimal? ChangePercent, bool? IsImprovement);

public sealed record MobileGeoPoint(double Latitude, double Longitude);

// ---------------------------------------------------------------- Home

public sealed record MobileHomeResponse(
    MobileGreeting Greeting,
    MobileQuickActionCounts QuickActions,
    MobileAssetOverviewCard AssetOverview,
    MobileMaintenancePerformanceCard MaintenancePerformance,
    IReadOnlyList<MobileUpcomingActivitySummary> UpcomingActivities,
    IReadOnlyList<MobileAlertItem> RecentAlerts,
    MobileAssetLocationCard AssetLocation,
    MobileAssetHealthCard AssetHealth);

/// <summary>"Good Morning, Raj — Here's what's happening today".</summary>
public sealed record MobileGreeting(string Salutation, string? UserName, string Message, DateTime GeneratedAtUtc);

/// <summary>Badge counts for the quick-action tiles.</summary>
public sealed record MobileQuickActionCounts(int OpenWorkOrders, int PendingInspections, int PendingApprovals, int CriticalAlerts);

public sealed record MobileAssetOverviewCard(int TotalAssets, IReadOnlyList<MobileBreakdownItem> Breakdown);

/// <summary>Headline is the planned share of work orders in the default window.</summary>
public sealed record MobileMaintenancePerformanceCard(decimal PlannedPercent, int Months, IReadOnlyList<MobileBreakdownItem> Breakdown);

/// <summary>A row of the Upcoming Activities card, e.g. "5 Work Orders — Today".</summary>
public sealed record MobileUpcomingActivitySummary(string Type, string Label, int Count, string When);

public sealed record MobileAssetLocationCard(int AssetsWithLocation, MobileGeoPoint? Center, IReadOnlyList<MobileAssetLocationItem> Assets);

public sealed record MobileAssetHealthCard(int TotalAssets, decimal HealthyPercent, IReadOnlyList<MobileBreakdownItem> Breakdown);

// ---------------------------------------------------------------- Asset overview

public sealed record MobileAssetOverviewResponse(
    int TotalAssets,
    IReadOnlyList<MobileBreakdownItem> ByStatus,
    IReadOnlyList<MobileBreakdownItem> ByType,
    IReadOnlyList<MobileBreakdownItem> ByLocation);

// ---------------------------------------------------------------- Maintenance performance

public sealed record MobileMaintenancePerformanceResponse(
    MobilePeriod Period,
    decimal PlannedPercent,
    MobileWorkOrderCounts Counts,
    IReadOnlyList<MobileBreakdownItem> Breakdown,
    IReadOnlyList<MobileWorkOrderTrendPoint> Trend,
    MobileMetricValue Mttr,
    MobileMetricValue Mtbf,
    IReadOnlyList<MobileAssetTypeMaintenance> ByAssetType);

public sealed record MobilePeriod(string Label, DateTime FromUtc, DateTime ToUtc);

public sealed record MobileWorkOrderCounts(int Total, int Planned, int Unplanned, int Overdue);

/// <summary>One month of the Work Order Trend chart; <see cref="Month"/> is yyyy-MM, <see cref="Label"/> e.g. "Jan".</summary>
public sealed record MobileWorkOrderTrendPoint(string Month, string Label, int Planned, int Unplanned, int Overdue);

public sealed record MobileAssetTypeMaintenance(string AssetType, int Total, int Planned, int Unplanned, int Overdue, decimal PlannedPercent);

// ---------------------------------------------------------------- Upcoming activities

public sealed record MobileUpcomingActivitiesResponse(
    DateOnly From,
    DateOnly To,
    int TotalCount,
    IReadOnlyList<MobileActivityDay> Days,
    IReadOnlyList<MobileActivityItem> Items);

/// <summary>One calendar day: activity counts per type (the coloured dots under each date).</summary>
public sealed record MobileActivityDay(DateOnly Date, int WorkOrders, int Inspections, int Approvals, int Total);

/// <summary>One activity in the list / calendar view. <see cref="Type"/> is WorkOrder, Inspection or Approval.</summary>
public sealed record MobileActivityItem(
    string Id,
    string Type,
    string Reference,
    string Title,
    string? AssetId,
    string? AssetName,
    string? Location,
    DateTime StartUtc,
    DateTime? EndUtc,
    DateOnly Date,
    string Status,
    string? Priority,
    string? AssignedTo);

// ---------------------------------------------------------------- My tasks

/// <summary>Open work orders, inspections and approvals assigned to the signed-in user.</summary>
public sealed record MobileMyTasksResponse(
    string? UserName,
    IReadOnlyList<string> MatchedAssigneeNames,
    MobileMyTaskCounts Counts,
    IReadOnlyList<MobileTaskItem> Items);

public sealed record MobileMyTaskCounts(int Total, int WorkOrders, int Inspections, int Approvals, int Overdue, int DueToday);

/// <summary>One task. <see cref="Type"/> is WorkOrder, Inspection or Approval; <see cref="DueUtc"/> is null when it has no date.</summary>
public sealed record MobileTaskItem(
    string Id,
    string Type,
    string Reference,
    string Title,
    string? AssetId,
    string? AssetName,
    string? Location,
    DateTime? DueUtc,
    DateOnly? DueDate,
    bool IsOverdue,
    string Status,
    string? Priority);

// ---------------------------------------------------------------- Recent alerts

public sealed record MobileRecentAlertsResponse(
    MobileAlertCounts Counts,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<MobileAlertItem> Items);

public sealed record MobileAlertCounts(int All, int Critical, int Warning, int Info);

/// <summary>
/// One alert in the feed. <see cref="Source"/> is IoT, WorkOrder, Inspection, SparePart or Maintenance;
/// <see cref="Severity"/> is Critical, Warning or Info.
/// </summary>
public sealed record MobileAlertItem(
    string Id,
    string Source,
    string Title,
    string? Reference,
    string? AssetId,
    string? AssetName,
    string Severity,
    string? Status,
    DateTime OccurredAtUtc);

// ---------------------------------------------------------------- Asset location

public sealed record MobileAssetLocationResponse(
    int TotalCount,
    MobileGeoPoint? Center,
    IReadOnlyList<MobileBreakdownItem> ByStatus,
    IReadOnlyList<MobileAssetLocationItem> Assets);

public sealed record MobileAssetLocationItem(
    string AssetRegistrationId,
    string? AssetTag,
    string? AssetName,
    string? Model,
    string? AssetType,
    string? Status,
    string StatusGroup,
    string? LocationLabel,
    double Latitude,
    double Longitude,
    string? ImageUrl);

// ---------------------------------------------------------------- Asset health

public sealed record MobileAssetHealthResponse(
    int TotalAssets,
    decimal HealthyPercent,
    IReadOnlyList<MobileBreakdownItem> Breakdown,
    IReadOnlyList<MobileHealthGroup> ByAssetType,
    IReadOnlyList<MobileHealthGroup> ByLocation,
    IReadOnlyList<MobileCriticalAsset> CriticalAssets);

public sealed record MobileHealthGroup(string Key, int Total, int Healthy, int Warning, int Critical, decimal HealthyPercent);

/// <summary>"Top Assets with Critical Health": the asset and the issue behind its score.</summary>
public sealed record MobileCriticalAsset(
    string AssetRegistrationId,
    string? AssetTag,
    string? AssetName,
    string? AssetType,
    decimal? HealthScore,
    string Issue);

// ---------------------------------------------------------------- Cost summary

public sealed record MobileCostSummaryResponse(
    string Currency,
    MobilePeriod Period,
    decimal TotalCost,
    decimal PreviousPeriodCost,
    decimal? ChangePercent,
    IReadOnlyList<MobileCostCategory> Categories,
    IReadOnlyList<MobileCostTrendPoint> MonthlyTrend,
    IReadOnlyList<MobileCostByAssetType> ByAssetType);

/// <summary>Key is maintenance (labour), operations (contractor / rental / freight) or spare-parts.</summary>
public sealed record MobileCostCategory(string Key, string Label, decimal Amount, decimal Percent);

public sealed record MobileCostTrendPoint(string Month, string Label, decimal Maintenance, decimal Operations, decimal SpareParts, decimal Total);

public sealed record MobileCostByAssetType(string AssetType, decimal Amount, decimal Percent);

// ---------------------------------------------------------------- Recent assets

public sealed record MobileRecentAssetsResponse(
    IReadOnlyList<MobileBreakdownItem> Counts,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<MobileRecentAssetItem> Items);

public sealed record MobileRecentAssetItem(
    string AssetRegistrationId,
    string? AssetTag,
    string? AssetName,
    string? Model,
    string? Manufacturer,
    string? Category,
    string? AssetType,
    string? Status,
    string StatusGroup,
    string? LocationLabel,
    string? ImageUrl,
    DateTime? CreatedAtUtc);
