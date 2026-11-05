using A360.Asset.Api.Contracts;
using A360.Asset.Repository.Dashboard;

namespace A360.Asset.Api.Endpoints;

/// <summary>
/// Asset dashboard: one read-only endpoint per widget, scoped to registered assets. Tracking data (devices, health,
/// firmware, sensors, location events, alerts, utilization, telemetry) is read from the shared database, not copied.
/// Quick Actions (navigation) and Asset Quick View (GET /api/asset-registrations/{id}) need nothing new.
/// </summary>
public static class AssetDashboardEndpoints
{
    private const string Online = "Online";
    private const string Idle = "Idle";
    private const string Offline = "Offline";
    private const string Alert = "Alert";
    private const string Unknown = "Unknown";
    private const string Resolved = "Resolved";
    private const string SensorAttention = "Attention";
    private const int DataFreshnessSlaMinutes = 60;
    private const int DefaultTopSites = 3;
    private const int DefaultAlertLimit = 10;

    private static readonly string[] LocationUpdateTypes = ["Entered Zone", "Departed Zone"];
    private static readonly string[] GeofenceEventTypes = ["Geofence Alert", "Dwell Exceeded"];

    public static RouteGroupBuilder MapAssetDashboardEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-dashboard").WithTags("Asset Dashboard");

        group.MapGet("/kpis", GetKpisAsync).WithName("GetAssetDashboardKpis");
        group.MapGet("/locations", GetLocationsAsync).WithName("GetAssetDashboardLocations");
        group.MapGet("/device-connectivity", GetDeviceConnectivityAsync).WithName("GetAssetDashboardDeviceConnectivity");
        group.MapGet("/firmware", GetFirmwareAsync).WithName("GetAssetDashboardFirmware");
        group.MapGet("/health-scores", GetHealthScoresAsync).WithName("GetAssetDashboardHealthScores");
        group.MapGet("/utilization", GetUtilizationAsync).WithName("GetAssetDashboardUtilization");
        group.MapGet("/events-timeline", GetEventsTimelineAsync).WithName("GetAssetDashboardEventsTimeline");
        group.MapGet("/sensors-attention", GetSensorsAttentionAsync).WithName("GetAssetDashboardSensorsAttention");
        group.MapGet("/alerts", GetAlertsAsync).WithName("GetAssetDashboardAlerts");

        return group;
    }

    /// <summary>
    /// 1 Connected Assets (registered assets whose asset tag reports in NetworkTimeSeries as meta.tagid), 2 Online Now, 3 Alerts in the period, 4 Coverage (% of connected assets
    /// with a location event in the last 24 h), 5 Data Freshness (% of reporting devices seen within the SLA of 60 min).
    /// </summary>
    private static async Task<IResult> GetKpisAsync(IAssetDashboardRepository repository, CancellationToken cancellationToken, int? days = null)
    {
        if (AssetDashboardPeriod.Invalid(days) is { } invalidDays)
        {
            return invalidDays;
        }

        var period = AssetDashboardPeriod.LastDays(days);
        var now = DateTime.UtcNow;
        var assets = await repository.GetAssetsAsync(cancellationToken);
        var assetIds = assets.Select(asset => asset.AssetRegistrationId).ToList();
        var devices = await repository.GetDevicesAsync(assetIds, cancellationToken);

        // 1. Connected assets: the asset tag (basic_information.asset_tag) matches a tag id in NetworkTimeSeries.
        //    Compared with those whose tag had already reported when the period started.
        var networkTags = await repository.GetNetworkTagsAsync(cancellationToken);
        var connectedAssets = assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.AssetTag) && networkTags.ContainsKey(asset.AssetTag.Trim()))
            .ToList();
        var connected = connectedAssets.Select(asset => asset.AssetRegistrationId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var connectedBefore = connectedAssets.Count(asset => networkTags[asset.AssetTag!.Trim()].FirstSeenAt < period.FromUtc);
        var connectedDetails = connectedAssets
            .Select(asset =>
            {
                var tag = networkTags[asset.AssetTag!.Trim()];
                return new ConnectedAssetDetail(asset.AssetRegistrationId, asset.AssetTag, asset.AssetName, asset.AssetType, asset.SiteName,
                    asset.Status, tag.LastStatus, tag.LastGatewayId, tag.FirstSeenAt, tag.LastSeenAt, tag.ReadingCount);
            })
            .OrderByDescending(detail => detail.LastSeenAt)
            .ThenBy(detail => detail.AssetName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Assets with a tracking device, as before; Online Now and Coverage are measured against these, unchanged.
        var withDevice = DistinctAssets(devices);

        // 2. Online now.
        var statuses = DeviceStatusByAsset(devices);
        var online = statuses.Count(entry => entry.Value == Online);

        // 3. Alerts raised in the period vs. the previous period.
        var alerts = await repository.GetAlertsAsync(assetIds, period.PreviousFromUtc, period.ToUtc, cancellationToken);
        var alertsNow = alerts.Count(alert => alert.RaisedAt >= period.FromUtc);
        var alertsBefore = alerts.Count - alertsNow;

        // 4. Coverage: assets with a device located in the last 24 h vs. the 24 h before.
        var keyToAsset = AssetKeys(assets.Where(asset => withDevice.Contains(asset.AssetRegistrationId)));
        var events = await repository.GetLocationEventsAsync(keyToAsset.Keys.ToList(), now.AddDays(-2), cancellationToken);
        var locatedLastDay = LocatedAssets(events.Where(locationEvent => locationEvent.EventTime >= now.AddDays(-1)), keyToAsset);
        var locatedDayBefore = LocatedAssets(events.Where(locationEvent => locationEvent.EventTime < now.AddDays(-1)), keyToAsset);
        var coverage = DashboardMath.Percent(locatedLastDay, withDevice.Count);
        var coverageBefore = DashboardMath.Percent(locatedDayBefore, withDevice.Count);

        // 5. Data freshness: of the devices that have ever reported, how many reported within the SLA.
        var reporting = devices.Where(device => device.LastSeenAt is not null).ToList();
        var fresh = reporting.Count(device => device.LastSeenAt >= now.AddMinutes(-DataFreshnessSlaMinutes));

        return Results.Ok(new AssetDashboardKpisResponse(
            period.Days,
            period.FromUtc,
            period.ToUtc,
            assets.Count,
            new AssetDashboardKpi(connected.Count, DashboardMath.Percent(connected.Count, assets.Count),
                DashboardMath.Change(connected.Count, connectedBefore), $"of {assets.Count} registered assets"),
            new AssetDashboardKpi(online, DashboardMath.Percent(online, withDevice.Count), null, "of connected assets"),
            new AssetDashboardKpi(alertsNow, null, DashboardMath.Change(alertsNow, alertsBefore), $"in last {period.Days} days"),
            new AssetDashboardKpi(coverage, coverage, coverage == 0 && coverageBefore == 0 ? null : Math.Round(coverage - coverageBefore, 1),
                "assets located within the last day (change in percentage points)"),
            new AssetDashboardKpi(DashboardMath.Percent(fresh, reporting.Count), DashboardMath.Percent(fresh, reporting.Count), null,
                $"{fresh} of {reporting.Count} reporting devices updated within {DataFreshnessSlaMinutes} min (SLA)"),
            connectedDetails));
    }

    /// <summary>6. Map pins, legend counts and the "Top Active Sites" panel; <paramref name="top"/> = 0 returns every site (View All).</summary>
    private static async Task<IResult> GetLocationsAsync(IAssetDashboardRepository repository, CancellationToken cancellationToken, int? top = null)
    {
        if (top is < 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["top"] = ["top must be 0 (all) or more"] });
        }

        var assets = await repository.GetAssetsAsync(cancellationToken);
        var assetIds = assets.Select(asset => asset.AssetRegistrationId).ToList();
        var devices = await repository.GetDevicesAsync(assetIds, cancellationToken);
        var deviceStatus = DeviceStatusByAsset(devices);
        var lastSeen = devices
            .Where(device => device.AssetRegistrationId is not null)
            .GroupBy(device => device.AssetRegistrationId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Max(device => device.LastSeenAt), StringComparer.OrdinalIgnoreCase);

        var openAlertAssets = (await repository.GetAlertsAsync(assetIds, null, null, cancellationToken))
            .Where(alert => alert.Status != Resolved && alert.AssetRegistrationId is not null)
            .Select(alert => alert.AssetRegistrationId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var keyToAsset = AssetKeys(assets);
        var lastZone = (await repository.GetLocationEventsAsync(keyToAsset.Keys.ToList(), DateTime.UtcNow.AddDays(-30), cancellationToken))
            .Where(locationEvent => keyToAsset.ContainsKey(locationEvent.AssetId))
            .GroupBy(locationEvent => keyToAsset[locationEvent.AssetId], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.MaxBy(locationEvent => locationEvent.EventTime)!.ZoneName, StringComparer.OrdinalIgnoreCase);

        string PinStatus(string assetId) => openAlertAssets.Contains(assetId) ? Alert : deviceStatus.GetValueOrDefault(assetId, Unknown);

        var pins = assets
            .Where(asset => asset.Latitude is not null && asset.Longitude is not null)
            .Select(asset => new AssetLocationPin(
                asset.AssetRegistrationId, asset.AssetTag, asset.AssetName, asset.AssetType, asset.SiteName,
                asset.Latitude!.Value, asset.Longitude!.Value, PinStatus(asset.AssetRegistrationId),
                lastZone.GetValueOrDefault(asset.AssetRegistrationId), lastSeen.GetValueOrDefault(asset.AssetRegistrationId)))
            .ToList();

        var legend = new[] { Online, Idle, Offline, Alert, Unknown }
            .ToDictionary(status => status, status => assets.Count(asset => PinStatus(asset.AssetRegistrationId) == status));

        // Sites rank by connected assets online, then by size; only assets with a device count towards a site.
        var sites = assets
            .Where(asset => deviceStatus.ContainsKey(asset.AssetRegistrationId))
            .GroupBy(asset => string.IsNullOrWhiteSpace(asset.SiteName) ? "Unassigned" : asset.SiteName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var siteStatuses = group.Select(asset => deviceStatus[asset.AssetRegistrationId]).ToList();
                var siteOnline = siteStatuses.Count(status => status == Online);
                return new AssetSiteActivity(
                    group.Key, group.Count(), DashboardMath.Percent(siteOnline, group.Count()),
                    siteOnline, siteStatuses.Count(status => status == Idle), siteStatuses.Count(status => status == Offline));
            })
            .OrderByDescending(site => site.Online)
            .ThenByDescending(site => site.TotalAssets)
            .ThenBy(site => site.SiteName)
            .ToList();

        var shownSites = (top ?? DefaultTopSites) == 0 ? sites : sites.Take(top ?? DefaultTopSites).ToList();
        return Results.Ok(new AssetLocationMapResponse(pins, legend, assets.Count - pins.Count, shownSites, sites.Count));
    }

    /// <summary>7. Device connectivity of the devices on registered assets: Online, Idle (warning), Offline, Unknown (no health record).</summary>
    private static async Task<IResult> GetDeviceConnectivityAsync(IAssetDashboardRepository repository, CancellationToken cancellationToken)
    {
        var assets = await repository.GetAssetsAsync(cancellationToken);
        var devices = await repository.GetDevicesAsync(assets.Select(asset => asset.AssetRegistrationId).ToList(), cancellationToken);
        var deviceIds = devices.Select(device => device.DeviceId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var health = (await repository.GetDeviceHealthAsync(deviceIds, cancellationToken))
            .GroupBy(record => record.DeviceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Connectivity, StringComparer.OrdinalIgnoreCase);

        var statuses = deviceIds.Select(deviceId => ConnectivityStatus(health.GetValueOrDefault(deviceId))).ToList();
        return Results.Ok(Donut(statuses, Online, [Online, Idle, Offline, Unknown]));
    }

    /// <summary>8. Share of devices on registered assets that run the latest firmware released for their model.</summary>
    private static async Task<IResult> GetFirmwareAsync(IAssetDashboardRepository repository, CancellationToken cancellationToken)
    {
        var assets = await repository.GetAssetsAsync(cancellationToken);
        var devices = await repository.GetDevicesAsync(assets.Select(asset => asset.AssetRegistrationId).ToList(), cancellationToken);
        var latest = await repository.GetLatestFirmwareByModelAsync(cancellationToken);

        var known = devices.Where(device => device.Model is not null && latest.ContainsKey(device.Model.Trim())).ToList();
        var models = known
            .GroupBy(device => device.Model!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var latestVersion = latest[group.Key].LatestVersion;
                return new FirmwareModelStatus(group.Key, latestVersion, group.Count(),
                    group.Count(device => SameVersion(device.FirmwareVersion, latestVersion)));
            })
            .OrderByDescending(model => model.Devices)
            .ToList();

        var onLatest = models.Sum(model => model.OnLatest);
        return Results.Ok(new AssetFirmwareStatusResponse(known.Count, onLatest, DashboardMath.Percent(onLatest, known.Count), devices.Count - known.Count, models));
    }

    /// <summary>9. Latest condition-assessment health score per asset: Healthy (≥ 70), Warning (40–69), Critical (&lt; 40), Unknown (never assessed).</summary>
    private static async Task<IResult> GetHealthScoresAsync(IAssetDashboardRepository repository, CancellationToken cancellationToken)
    {
        var assets = await repository.GetAssetsAsync(cancellationToken);
        var scores = await repository.GetLatestHealthScoresAsync(cancellationToken);

        var bands = assets.Select(asset => scores.TryGetValue(asset.AssetRegistrationId, out var score)
                ? score >= 70 ? "Healthy" : score >= 40 ? "Warning" : "Critical"
                : Unknown)
            .ToList();

        return Results.Ok(Donut(bands, "Healthy", ["Healthy", "Warning", "Critical", Unknown]));
    }

    /// <summary>10. Running % vs. idle % per asset type over the last <paramref name="days"/> days (default 7); type comes from the asset registration.</summary>
    private static async Task<IResult> GetUtilizationAsync(IAssetDashboardRepository repository, CancellationToken cancellationToken, int? days = null)
    {
        if (AssetDashboardPeriod.Invalid(days) is { } invalidDays)
        {
            return invalidDays;
        }

        var period = AssetDashboardPeriod.LastDays(days);
        var assets = await repository.GetAssetsAsync(cancellationToken);
        var assetType = assets.ToDictionary(asset => asset.AssetRegistrationId, asset => asset.AssetType, StringComparer.OrdinalIgnoreCase);
        var records = await repository.GetUtilizationAsync(assetType.Keys.ToList(), period.FromUtc, period.ToUtc, cancellationToken);

        string TypeOf(DashboardUtilization record) =>
            assetType.GetValueOrDefault(record.AssetRegistrationId) ?? record.AssetType ?? "Unclassified";

        var items = records
            .GroupBy(TypeOf, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                double running = group.Sum(record => record.RunningMinutes);
                double idle = group.Sum(record => record.IdleMinutes);
                var runningPercent = DashboardMath.Percent(running, running + idle);
                return new AssetTypeUtilization(
                    group.Key,
                    group.Select(record => record.AssetRegistrationId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    Math.Round(running / 60, 1),
                    Math.Round(idle / 60, 1),
                    runningPercent,
                    running + idle == 0 ? 0 : Math.Round(100 - runningPercent, 1));
            })
            .OrderByDescending(item => item.RunningPercent)
            .ThenBy(item => item.AssetType)
            .ToList();

        double totalRunning = records.Sum(record => record.RunningMinutes);
        double totalIdle = records.Sum(record => record.IdleMinutes);
        var overallRunning = DashboardMath.Percent(totalRunning, totalRunning + totalIdle);

        return Results.Ok(new AssetUtilizationByTypeResponse(
            period.Days, period.FromUtc, period.ToUtc, overallRunning,
            totalRunning + totalIdle == 0 ? 0 : Math.Round(100 - overallRunning, 1), items));
    }

    /// <summary>11. Per-day counts for registered assets over the last <paramref name="days"/> days (7 / 30 / 90 on the dashboard), zero-filled.</summary>
    private static async Task<IResult> GetEventsTimelineAsync(IAssetDashboardRepository repository, CancellationToken cancellationToken, int? days = null)
    {
        if (AssetDashboardPeriod.Invalid(days) is { } invalidDays)
        {
            return invalidDays;
        }

        var period = AssetDashboardPeriod.LastDays(days);
        var assets = await repository.GetAssetsAsync(cancellationToken);
        var assetIds = assets.Select(asset => asset.AssetRegistrationId).ToList();

        var telemetry = ByDay(await repository.CountTelemetryPerDayAsync(assetIds, period.FromUtc, period.ToUtc, cancellationToken));
        var alerts = ByDay(await repository.CountAlertsPerDayAsync(assetIds, period.FromUtc, period.ToUtc, cancellationToken));
        var locationEvents = await repository.CountLocationEventsPerDayAsync(AssetKeys(assets).Keys.ToList(), period.FromUtc, period.ToUtc, cancellationToken);
        var locationUpdates = ByDay(locationEvents.Where(row => LocationUpdateTypes.Contains(row.Key, StringComparer.OrdinalIgnoreCase)));
        var geofenceEvents = ByDay(locationEvents.Where(row => GeofenceEventTypes.Contains(row.Key, StringComparer.OrdinalIgnoreCase)));

        var points = period.EachDay()
            .Select(day => new AssetEventsTimelinePoint(day, telemetry.GetValueOrDefault(day), locationUpdates.GetValueOrDefault(day),
                geofenceEvents.GetValueOrDefault(day), alerts.GetValueOrDefault(day)))
            .ToList();

        var totals = new AssetEventsTimelinePoint("total", points.Sum(point => point.TelemetryData), points.Sum(point => point.LocationUpdates),
            points.Sum(point => point.GeofenceEvents), points.Sum(point => point.Alerts));

        return Results.Ok(new AssetEventsTimelineResponse(period.Days, period.FromUtc, period.ToUtc, totals, points));
    }

    /// <summary>12. Sensors on registered assets whose status is Attention (abnormal reading).</summary>
    private static async Task<IResult> GetSensorsAttentionAsync(IAssetDashboardRepository repository, CancellationToken cancellationToken)
    {
        var assets = await repository.GetAssetsAsync(cancellationToken);
        var names = assets.ToDictionary(asset => asset.AssetRegistrationId, asset => asset.AssetName, StringComparer.OrdinalIgnoreCase);
        var sensors = await repository.GetSensorsAsync(names.Keys.ToList(), SensorAttention, cancellationToken);

        return Results.Ok(new SensorsAttentionResponse(sensors.Count, sensors
            .Select(sensor => new AttentionSensor(sensor.SensorId, sensor.Name, sensor.SensorType, sensor.MeasuredParameter, sensor.LiveReading,
                sensor.Unit, sensor.DeviceId, sensor.AssetRegistrationId, names.GetValueOrDefault(sensor.AssetRegistrationId ?? string.Empty)))
            .ToList()));
    }

    /// <summary>14. Alerts on registered assets, newest first; asset name and site come from the asset registration.</summary>
    private static async Task<IResult> GetAlertsAsync(
        IAssetDashboardRepository repository,
        CancellationToken cancellationToken,
        int? days = null,
        string? status = null,
        string? severity = null,
        int? limit = null)
    {
        if (AssetDashboardPeriod.Invalid(days) is { } invalidDays)
        {
            return invalidDays;
        }

        if (limit is < 1)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["limit"] = ["limit must be at least 1"] });
        }

        var assets = (await repository.GetAssetsAsync(cancellationToken))
            .ToDictionary(asset => asset.AssetRegistrationId, StringComparer.OrdinalIgnoreCase);
        var period = days is null ? null : AssetDashboardPeriod.LastDays(days);
        var alerts = await repository.GetAlertsAsync(assets.Keys.ToList(), period?.FromUtc, period?.ToUtc, cancellationToken);

        return Results.Ok(alerts
            .Where(alert => string.IsNullOrWhiteSpace(status) || string.Equals(alert.Status, status.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(alert => string.IsNullOrWhiteSpace(severity) || string.Equals(alert.Severity, severity.Trim(), StringComparison.OrdinalIgnoreCase))
            .Take(limit ?? DefaultAlertLimit)
            .Select(alert =>
            {
                var asset = assets.GetValueOrDefault(alert.AssetRegistrationId ?? string.Empty);
                return new AssetDashboardAlert(alert.AlertId, alert.RaisedAt, alert.AssetRegistrationId, asset?.AssetName ?? alert.AssetName,
                    asset?.SiteName ?? alert.SiteName, alert.EventType, alert.Severity, alert.Status, alert.DeviceId, alert.Message);
            })
            .ToList());
    }

    // Device connectivity Online / Warning / Offline maps to the dashboard's Online / Idle / Offline.
    private static string ConnectivityStatus(string? connectivity) => connectivity?.Trim().ToLowerInvariant() switch
    {
        "online" => Online,
        "warning" or "idle" => Idle,
        "offline" => Offline,
        _ => Unknown
    };

    /// <summary>One status per connected asset: Online if any of its devices is online, else Idle, else Offline (else Unknown).</summary>
    private static Dictionary<string, string> DeviceStatusByAsset(IEnumerable<DashboardDevice> devices)
    {
        string[] rank = [Online, Idle, Offline, Unknown];
        return devices
            .Where(device => !string.IsNullOrWhiteSpace(device.AssetRegistrationId))
            .GroupBy(device => device.AssetRegistrationId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(device => ConnectivityStatus(device.Connectivity)).MinBy(status => Array.IndexOf(rank, status))!,
                StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> DistinctAssets(IEnumerable<DashboardDevice> devices)
    {
        return devices
            .Where(device => !string.IsNullOrWhiteSpace(device.AssetRegistrationId))
            .Select(device => device.AssetRegistrationId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Location events name an asset by registration id or by tag; maps both to the registration id.</summary>
    private static Dictionary<string, string> AssetKeys(IEnumerable<DashboardAsset> assets)
    {
        var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets)
        {
            keys.TryAdd(asset.AssetRegistrationId, asset.AssetRegistrationId);
            if (!string.IsNullOrWhiteSpace(asset.AssetTag))
            {
                keys.TryAdd(asset.AssetTag.Trim(), asset.AssetRegistrationId);
            }
        }

        return keys;
    }

    private static int LocatedAssets(IEnumerable<DashboardLocationEvent> events, IReadOnlyDictionary<string, string> keyToAsset)
    {
        return events
            .Where(locationEvent => keyToAsset.ContainsKey(locationEvent.AssetId))
            .Select(locationEvent => keyToAsset[locationEvent.AssetId])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    private static DashboardDonutResponse Donut(IReadOnlyCollection<string> values, string centre, IReadOnlyList<string> bands)
    {
        return new DashboardDonutResponse(
            values.Count,
            DashboardMath.Percent(values.Count(value => value == centre), values.Count),
            bands.Select(band =>
            {
                var count = values.Count(value => value == band);
                return new DashboardBand(band, count, DashboardMath.Percent(count, values.Count));
            }).ToList());
    }

    private static bool SameVersion(string? installed, string latest)
    {
        static string Normalize(string value) => value.Trim().TrimStart('v', 'V');
        return installed is not null && string.Equals(Normalize(installed), Normalize(latest), StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, int> ByDay(IEnumerable<DashboardDailyCount> counts)
    {
        return counts.GroupBy(row => row.Date).ToDictionary(group => group.Key, group => group.Sum(row => row.Count));
    }
}
