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
    AssetDashboardKpi DataFreshness);

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

public sealed record AssetSiteActivity(
    string SiteName,
    int TotalAssets,
    double OnlinePercent,
    int Online,
    int Idle,
    int Offline);

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
