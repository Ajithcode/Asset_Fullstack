using Microsoft.AspNetCore.Mvc;

namespace A360.Asset.Api.Contracts;

/// <summary>
/// The "last N days" window of the Asset dashboard: whole UTC days ending with today.
/// For days = 7 on 2 Nov the window is 27 Oct 00:00 to 3 Nov 00:00 (exclusive); the previous period is the 7 days before.
/// </summary>
public sealed record AssetDashboardPeriod(int Days, DateTime FromUtc, DateTime ToUtc)
{
    public const int DefaultDays = 7;
    public const int MaxDays = 365;

    public DateTime PreviousFromUtc => FromUtc.AddDays(-Days);

    public static AssetDashboardPeriod LastDays(int? days)
    {
        var length = days ?? DefaultDays;
        var toUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(1), DateTimeKind.Utc);
        return new AssetDashboardPeriod(length, toUtc.AddDays(-length), toUtc);
    }

    /// <summary>Null when valid; otherwise a validation problem for the "days" query parameter.</summary>
    public static IResult? Invalid(int? days)
    {
        return days is null or (>= 1 and <= MaxDays)
            ? null
            : Results.ValidationProblem(new Dictionary<string, string[]> { ["days"] = [$"days must be between 1 and {MaxDays}"] });
    }

    public IEnumerable<string> EachDay()
    {
        for (var day = FromUtc; day < ToUtc; day = day.AddDays(1))
        {
            yield return day.ToString("yyyy-MM-dd");
        }
    }
}

/// <summary>
/// The IoT &amp; Tracking Overview's Site / Asset Type / Status dropdowns, sent as query parameters to every widget endpoint
/// (with days for the period). Each narrows the registered assets the widget is built from; empty or "all" means no filter.
/// Status is the asset's network status: Online, Offline, Idle or Unknown (see GET /api/asset-dashboard/filters).
/// </summary>
public sealed class AssetDashboardFilter
{
    public static readonly string[] Statuses = ["Online", "Offline", "Idle", "Unknown"];

    /// <summary>Site id (location.site_id); "unassigned" for assets without a site.</summary>
    [FromQuery(Name = "siteId")]
    public string? SiteId { get; init; }

    /// <summary>Asset type as stored on the asset registration (classification.asset_type).</summary>
    [FromQuery(Name = "assetType")]
    public string? AssetType { get; init; }

    [FromQuery(Name = "status")]
    public string? Status { get; init; }

    public string? Site => Value(SiteId);

    public string? Type => Value(AssetType);

    /// <summary>The status in its standard spelling, or null when not filtering by status.</summary>
    public string? State => Value(Status) is { } status ? Statuses.FirstOrDefault(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase)) : null;

    /// <summary>True when any of the three dropdowns is set.</summary>
    public bool IsSet => Site is not null || Type is not null || State is not null;

    /// <summary>Null when valid; otherwise a validation problem for the "status" query parameter.</summary>
    public IResult? Invalid()
    {
        return Value(Status) is { } status && State is null
            ? Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = [$"status must be one of: {string.Join(", ", Statuses)} (or omitted for all)"] })
            : null;
    }

    private static string? Value(string? value) =>
        string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), "all", StringComparison.OrdinalIgnoreCase) ? null : value.Trim();
}

// ---- Filter dropdowns ----

public sealed record DashboardSiteOption(string SiteId, string SiteName, string? Code, int AssetCount);

public sealed record DashboardAssetTypeOption(string AssetType, int AssetCount);

public sealed record AssetDashboardFiltersResponse(
    IReadOnlyList<int> Days,
    IReadOnlyList<DashboardSiteOption> Sites,
    IReadOnlyList<DashboardAssetTypeOption> AssetTypes,
    IReadOnlyList<string> Statuses);

internal static class DashboardMath
{
    /// <summary>part / total in percent, 1 decimal; 0 when total is 0.</summary>
    public static double Percent(double part, double total) => total <= 0 ? 0 : Math.Round(part * 100 / total, 1);

    /// <summary>Change vs. the previous value in percent, 1 decimal; null when the previous value was 0.</summary>
    public static double? Change(double current, double previous) => previous == 0 ? null : Math.Round((current - previous) * 100 / previous, 1);
}

// ---- 1–5: KPI cards ----

/// <param name="Value">The headline number of the card.</param>
/// <param name="Percent">The card's percentage line (e.g. Online Now as % of connected assets); null when the card has none.</param>
/// <param name="ChangePercent">Change vs. the previous period; null when not applicable or the previous value was 0.</param>
public sealed record AssetDashboardKpi(double Value, double? Percent, double? ChangePercent, string? Note);

/// <summary>The five KPI cards: Connected Assets, Online Now, Alerts, Coverage, Data Freshness.</summary>
public sealed record AssetDashboardKpisResponse(
    int Days,
    DateTime From,
    DateTime To,
    int TotalAssets,
    AssetDashboardKpi ConnectedAssets,
    AssetDashboardKpi OnlineNow,
    AssetDashboardKpi Alerts,
    AssetDashboardKpi Coverage,
    AssetDashboardKpi DataFreshness,
    IReadOnlyList<ConnectedAssetDetail> ConnectedAssetDetails);

/// <summary>A registered asset whose asset tag reports in NetworkTimeSeries (meta.tagid).</summary>
/// <param name="AssetStatus">The asset's registration status (basic_information.status).</param>
/// <param name="NetworkStatus">Status of the tag's newest network reading, e.g. "online" or "offline".</param>
/// <param name="ReadingCount">How many network readings the tag has.</param>
public sealed record ConnectedAssetDetail(
    string AssetRegistrationId,
    string? AssetTag,
    string? AssetName,
    string? AssetType,
    string? SiteName,
    string? AssetStatus,
    string? NetworkStatus,
    string? GatewayId,
    DateTime? FirstSeenAt,
    DateTime? LastSeenAt,
    long ReadingCount);

// ---- 6: Real-time Asset Location ----

/// <param name="Status">Online, Idle, Offline or Alert (an open alert wins over the device status); Unknown when the asset has no device.</param>
public sealed record AssetLocationPin(
    string AssetRegistrationId,
    string? AssetTag,
    string? AssetName,
    string? AssetType,
    string? SiteName,
    double Latitude,
    double Longitude,
    string Status,
    string? LastZone,
    DateTime? LastSeenAt);

/// <summary>One site in "Top Active Sites": the assets mapped to it (location.site_id) and their network status.</summary>
/// <param name="SiteId">The site's id in master_sites; "unassigned" for assets with no site. Pass it to GET /api/asset-dashboard/sites/{siteId}.</param>
/// <param name="SiteName">Name from master_sites, or the name stored on the assets when the site is not in master data.</param>
/// <param name="TotalAssets">Every registered asset mapped to the site.</param>
/// <param name="OnlinePercent">Online as a percentage of <paramref name="TotalAssets"/>.</param>
/// <param name="Online">Tag reported within the last 10 minutes and its latest status is "online".</param>
/// <param name="Offline">Tag reported within the last 10 minutes and its latest status is not "online".</param>
/// <param name="Idle">Tag has readings, but none in the last 10 minutes.</param>
/// <param name="Unknown">Asset whose tag has no readings in NetworkTimeSeries.</param>
public sealed record AssetSiteActivity(
    string SiteId,
    string SiteName,
    string? SiteCode,
    int TotalAssets,
    double OnlinePercent,
    int Online,
    int Idle,
    int Offline,
    int Unknown);

/// <summary>An asset mapped to a site, with its tag's latest NetworkTimeSeries reading.</summary>
/// <param name="NetworkStatus">Online, Offline, Idle (no reading for 10 minutes) or Unknown (tag has no readings).</param>
public sealed record SiteAssetDetail(
    string AssetRegistrationId,
    string? AssetTag,
    string? AssetName,
    string? AssetType,
    string? AssetStatus,
    string? BuildingName,
    string? FloorName,
    string? ZoneName,
    string? LocationPath,
    double? Latitude,
    double? Longitude,
    string NetworkStatus,
    string? GatewayId,
    DateTime? LastSeenAt);

/// <summary>GET /api/asset-dashboard/sites/{siteId}: the site, its counts, and every asset mapped to it.</summary>
public sealed record AssetSiteDetailResponse(
    string SiteId,
    string SiteName,
    string? SiteCode,
    string? SiteType,
    string? City,
    string? Country,
    int TotalAssets,
    double OnlinePercent,
    IReadOnlyList<SiteStatusGroup> Statuses);

/// <summary>Every site with its asset counts and assets (GET /api/asset-dashboard/sites).</summary>
/// <param name="TotalAssets">Assets across all returned sites.</param>
public sealed record AllSitesAssetsResponse(int TotalSites, int TotalAssets, IReadOnlyList<AssetSiteDetailResponse> Sites);

/// <summary>One status in a site (Online, Idle, Offline, Unknown): its count and the assets behind it. Highest count first.</summary>
public sealed record SiteStatusGroup(string Status, int Count, IReadOnlyList<SiteAssetDetail> Assets);

/// <summary>GET /api/asset-dashboard/tags/{tagId}: the tag's network details and the asset(s) it is mapped to.</summary>
/// <param name="IsMapped">True when an asset's basic_information.asset_tag equals this tag id.</param>
/// <param name="NetworkStatus">Online / Offline / Idle (no reading for 10 minutes) for a mapped tag; Unknown when not mapped.</param>
/// <param name="LastStatus">Status on the newest NetworkTimeSeries reading, as stored (e.g. "online").</param>
/// <param name="RecentReadings">Newest readings first, as many as the readings query parameter asks for.</param>
public sealed record TagDetailResponse(
    string TagId,
    bool IsMapped,
    string NetworkStatus,
    string? LastStatus,
    string? LastGatewayId,
    double? LastRssi,
    DateTime? FirstSeenAt,
    DateTime? LastSeenAt,
    long ReadingCount,
    IReadOnlyList<TagReading> RecentReadings,
    IReadOnlyList<TagMappedAsset> MappedAssets);

public sealed record TagReading(DateTime? Timestamp, string? GatewayId, string? Status, double? Rssi, string? Raw);

/// <summary>An asset whose asset tag is the requested tag id.</summary>
public sealed record TagMappedAsset(
    string AssetRegistrationId,
    string? AssetTag,
    string? AssetName,
    string? AssetType,
    string? AssetStatus,
    string? SiteId,
    string? SiteName,
    string? BuildingName,
    string? FloorName,
    string? ZoneName,
    string? LocationPath,
    double? Latitude,
    double? Longitude);

/// <summary>GET /api/asset-dashboard/categories: asset count per classification.category, highest first.</summary>
/// <param name="TotalAssets">All registered (not deleted) assets.</param>
public sealed record AssetCategoryCountsResponse(int TotalAssets, IReadOnlyList<AssetCategoryCount> Categories);

/// <param name="Category">classification.category; "Uncategorized" when an asset has none.</param>
/// <param name="SubCategories">Counts per classification.sub_category inside this category, highest first.</param>
public sealed record AssetCategoryCount(string Category, int Count, double Percent, IReadOnlyList<AssetSubCategoryCount> SubCategories);

public sealed record AssetSubCategoryCount(string SubCategory, int Count);

/// <summary>
/// GET /api/asset-dashboard/critical-assets, /assets-without-custodian, /assets-without-documents:
/// the matching assets and their count.
/// </summary>
/// <param name="Count">Assets that match.</param>
/// <param name="TotalAssets">All registered (not deleted) assets.</param>
public sealed record AssetCheckResponse(int Count, int TotalAssets, IReadOnlyList<AssetCheckDetail> Assets);

/// <param name="CustodianName">location_assignment.assignment.assigned_to_name.</param>
/// <param name="DocumentCount">Files in purchase_warranty.documents + additional_information.attachments + AssetDocument records.</param>
public sealed record AssetCheckDetail(
    string AssetRegistrationId,
    string? AssetTag,
    string? AssetName,
    string? Category,
    string? SubCategory,
    string? AssetType,
    string? Criticality,
    string? AssetStatus,
    string? SiteId,
    string? SiteName,
    string? LocationPath,
    string? CustodianName,
    string? CustodianDepartment,
    int DocumentCount);

/// <summary>GET /api/asset-dashboard/statuses: every asset status with its asset count and assets.</summary>
/// <param name="TotalAssets">All registered (not deleted) assets.</param>
/// <param name="TotalStatuses">Statuses in <see cref="Statuses"/>.</param>
public sealed record AssetStatusCountsResponse(int TotalAssets, int TotalStatuses, IReadOnlyList<AssetStatusCount> Statuses);

/// <param name="Status">A standard status (Active, Inactive, In Storage, Under Maintenance, In Repair, Out of Service,
/// Disposed), another basic_information.status value found on assets, or "No Status" for assets without one.</param>
/// <param name="Percent">Share of all registered assets, 0-100 with one decimal.</param>
/// <param name="Assets">The assets with this status, by name; empty when includeAssets=false.</param>
public sealed record AssetStatusCount(string Status, int Count, double Percent, IReadOnlyList<AssetCheckDetail> Assets);

/// <summary>GET /api/asset-dashboard/assets/{assetRegistrationId}/counts: the summary cards on the asset detail page.</summary>
/// <param name="Relationships">Related-asset links: sub assets under this asset, plus links naming it as a sub asset.</param>
/// <param name="Documents">Asset Documents records plus files attached on the registration.</param>
/// <param name="WorkOrders">Maintenance work orders raised on the asset (matched by registration id or tag).</param>
/// <param name="ServiceRequests">Service requests raised on the asset, any status.</param>
/// <param name="OpenAlerts">IoT alerts on the asset that are not Resolved.</param>
public sealed record AssetSummaryCountsResponse(
    string AssetRegistrationId,
    string? AssetTag,
    string? AssetName,
    int Components,
    int Relationships,
    int Documents,
    int Images,
    int WorkOrders,
    int ServiceRequests,
    int OpenAlerts);

/// <param name="Legend">Counts per pin status: Online, Idle, Offline, Alert, Unknown.</param>
/// <param name="AssetsWithoutCoordinates">Assets that cannot be pinned because no latitude / longitude is recorded.</param>
/// <param name="TotalSites">All sites, so the UI can show "View All" when <see cref="TopSites"/> is cut to the top N.</param>
public sealed record AssetLocationMapResponse(
    IReadOnlyList<AssetLocationPin> Pins,
    IReadOnlyDictionary<string, int> Legend,
    int AssetsWithoutCoordinates,
    IReadOnlyList<AssetSiteActivity> TopSites,
    int TotalSites);

// ---- 7: Device Connectivity, 9: Asset Health Score, shared donut shape ----

public sealed record DashboardBand(string Name, int Count, double Percent);

/// <param name="CentrePercent">The number in the middle of the donut (Online % or Healthy %).</param>
public sealed record DashboardDonutResponse(int Total, double CentrePercent, IReadOnlyList<DashboardBand> Bands);

// ---- 8: Firmware Updates ----

public sealed record FirmwareModelStatus(string DeviceModel, string LatestVersion, int Devices, int OnLatest);

/// <param name="DevicesWithoutFirmwareInfo">Devices whose model has no firmware release recorded, so they are not counted.</param>
public sealed record AssetFirmwareStatusResponse(
    int TotalDevices,
    int OnLatest,
    double Percent,
    int DevicesWithoutFirmwareInfo,
    IReadOnlyList<FirmwareModelStatus> Models);

// ---- 10: Utilization Rate (by Asset Type) ----

public sealed record AssetTypeUtilization(string AssetType, int AssetCount, double RunningHours, double IdleHours, double RunningPercent, double IdlePercent);

public sealed record AssetUtilizationByTypeResponse(
    int Days,
    DateTime From,
    DateTime To,
    double OverallRunningPercent,
    double OverallIdlePercent,
    IReadOnlyList<AssetTypeUtilization> Items);

// ---- 11: IoT Events Timeline ----

public sealed record AssetEventsTimelinePoint(string Date, int TelemetryData, int LocationUpdates, int GeofenceEvents, int Alerts);

public sealed record AssetEventsTimelineResponse(
    int Days,
    DateTime From,
    DateTime To,
    AssetEventsTimelinePoint Totals,
    IReadOnlyList<AssetEventsTimelinePoint> Points);

// ---- Location updates: gateway changes per tag ----

/// <param name="LastFromLatitude">Where the tag was (reading before the last change); null when the reading had no coordinates.</param>
/// <param name="LastToLatitude">Where the tag moved to (reading of the last change); null when the reading had no coordinates.</param>
public sealed record TagLocationUpdates(
    string TagId,
    long Count,
    DateTime? LastChangedAt,
    string? LastFromGatewayId,
    string? LastToGatewayId,
    double? LastFromLatitude,
    double? LastFromLongitude,
    double? LastToLatitude,
    double? LastToLongitude);

/// <param name="Days">The window in days; null when no window was asked for (all time).</param>
/// <param name="TotalCount">Gateway changes across every tag.</param>
/// <param name="TagCount">Tags that changed gateway at least once.</param>
public sealed record AssetLocationUpdatesResponse(
    int? Days,
    DateTime? From,
    DateTime? To,
    long TotalCount,
    int TagCount,
    IReadOnlyList<TagLocationUpdates> Tags);

// ---- 12: Sensors Requiring Attention ----

public sealed record AttentionSensor(
    string SensorId,
    string? Name,
    string? SensorType,
    string? MeasuredParameter,
    decimal? LiveReading,
    string? Unit,
    string? DeviceId,
    string? AssetRegistrationId,
    string? AssetName);

public sealed record SensorsAttentionResponse(int Count, IReadOnlyList<AttentionSensor> Sensors);

// ---- 14: Recent Alerts & Exceptions ----

/// <remarks>Acknowledge / resolve actions use the Tracking API: PATCH /api/iot-alerts/{alertId}/status.</remarks>
public sealed record AssetDashboardAlert(
    string AlertId,
    DateTime RaisedAt,
    string? AssetRegistrationId,
    string? AssetName,
    string? SiteName,
    string? EventType,
    string? Severity,
    string? Status,
    string? DeviceId,
    string? Message);
