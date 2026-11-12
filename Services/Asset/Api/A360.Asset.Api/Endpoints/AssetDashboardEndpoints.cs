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
    private const string UnassignedSiteId = "unassigned";
    private const string NoStatus = "No Status";
    private const string SensorAttention = "Attention";
    private const int DataFreshnessSlaMinutes = 60;

    // Device Connectivity: a mapped tag with no reading for this long counts as Idle.
    private const int IdleAfterMinutes = 10;
    private const int DefaultTagReadings = 20;
    private const string Uncategorized = "Uncategorized";
    private const string CriticalLevel = "Critical";
    private const int MaxTagReadings = 500;
    private const int DefaultTopSites = 3;
    private const int DefaultAlertLimit = 10;

    private static readonly string[] LocationUpdateTypes = ["Entered Zone", "Departed Zone"];
    private static readonly string[] GeofenceEventTypes = ["Geofence Alert", "Dwell Exceeded"];

    public static RouteGroupBuilder MapAssetDashboardEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-dashboard").WithTags("Asset Dashboard");

        group.MapGet("/filters", GetFiltersAsync).WithName("GetAssetDashboardFilters");
        group.MapGet("/kpis", GetKpisAsync).WithName("GetAssetDashboardKpis");
        group.MapGet("/locations", GetLocationsAsync).WithName("GetAssetDashboardLocations");
        group.MapGet("/sites", GetAllSiteAssetsAsync).WithName("GetAssetDashboardAllSiteAssets");
        group.MapGet("/sites/{siteId}", GetSiteAssetsAsync).WithName("GetAssetDashboardSiteAssets");
        group.MapGet("/tags/{tagId}", GetTagAsync).WithName("GetAssetDashboardTag");
        group.MapGet("/categories", GetCategoryCountsAsync).WithName("GetAssetDashboardCategoryCounts");
        group.MapGet("/critical-assets", GetCriticalAssetsAsync).WithName("GetAssetDashboardCriticalAssets");
        group.MapGet("/assets-without-custodian", GetAssetsWithoutCustodianAsync).WithName("GetAssetDashboardAssetsWithoutCustodian");
        group.MapGet("/assets-without-documents", GetAssetsWithoutDocumentsAsync).WithName("GetAssetDashboardAssetsWithoutDocuments");
        group.MapGet("/device-connectivity", GetDeviceConnectivityAsync).WithName("GetAssetDashboardDeviceConnectivity");
        group.MapGet("/firmware", GetFirmwareAsync).WithName("GetAssetDashboardFirmware");
        group.MapGet("/health-scores", GetHealthScoresAsync).WithName("GetAssetDashboardHealthScores");
        group.MapGet("/utilization", GetUtilizationAsync).WithName("GetAssetDashboardUtilization");
        group.MapGet("/events-timeline", GetEventsTimelineAsync).WithName("GetAssetDashboardEventsTimeline");
        group.MapGet("/location-updates", GetLocationUpdatesAsync).WithName("GetAssetDashboardLocationUpdates");
        group.MapGet("/sensors-attention", GetSensorsAttentionAsync).WithName("GetAssetDashboardSensorsAttention");
        group.MapGet("/alerts", GetAlertsAsync).WithName("GetAssetDashboardAlerts");
        group.MapGet("/statuses", GetStatusCountsAsync).WithName("GetAssetDashboardStatusCounts");
        group.MapGet("/assets/{assetRegistrationId}/counts", GetAssetSummaryCountsAsync).WithName("GetAssetDashboardAssetSummaryCounts");

        return group;
    }

    /// <summary>
    /// 1 Connected Assets (registered assets whose asset tag reports in NetworkTimeSeries as meta.tagid), 2 Online Now (connected
    /// assets whose network status is Online), 3 Alerts in the period, 4 Coverage (% of gateways in NetworkTimeSeries whose
    /// status, the status field of the gateway's newest reading, is "online"), 5 Data Freshness (% of registered asset tags
    /// with a reading in the last <see cref="DataFreshnessSlaMinutes"/> minutes). Coverage and Data Freshness compare with the
    /// same moment <paramref name="days"/> days ago, in percentage points.
    /// </summary>
    private static async Task<IResult> GetKpisAsync(IAssetDashboardRepository repository, [AsParameters] AssetDashboardFilter filter, CancellationToken cancellationToken, int? days = null)
    {
        if (filter.Invalid() is { } invalidFilter)
        {
            return invalidFilter;
        }

        if (AssetDashboardPeriod.Invalid(days) is { } invalidDays)
        {
            return invalidDays;
        }

        var period = AssetDashboardPeriod.LastDays(days);
        var now = DateTime.UtcNow;
        var networkTags = await repository.GetNetworkTagsAsync(cancellationToken);
        var assets = FilterAssets(await repository.GetAssetsAsync(cancellationToken), filter, networkTags);
        var assetIds = assets.Select(asset => asset.AssetRegistrationId).ToList();

        // 1. Connected assets: the asset tag (basic_information.asset_tag) matches a tag id in NetworkTimeSeries.
        //    Compared with those whose tag had already reported when the period started.
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

        // 2. Online now: connected assets whose network status is Online (newest reading "online" and within the idle
        //    window), the same rule as Device Connectivity and the Status filter.
        var online = connectedAssets.Count(asset => NetworkState(NetworkTag(asset, networkTags)) == Online);

        // 3. Alerts raised in the period vs. the previous period.
        var alerts = await repository.GetAlertsAsync(assetIds, period.PreviousFromUtc, period.ToUtc, cancellationToken);
        var alertsNow = alerts.Count(alert => alert.RaisedAt >= period.FromUtc);
        var alertsBefore = alerts.Count - alertsNow;

        // Coverage and Data Freshness are compared with the same moment one period ago.
        var past = now.AddDays(-period.Days);
        var assetTags = TagsOf(assets);
        // Without filters every gateway counts; with them, the gateways that have carried the matching assets' tags.
        var scopeTags = filter.IsSet ? assetTags : null;
        var allTime = await repository.GetNetworkActivityAsync(null, null, scopeTags, cancellationToken);
        // Everything up to that moment: each gateway's status then, and each tag's last reading then.
        var atPast = await repository.GetNetworkActivityAsync(null, past, scopeTags, cancellationToken);

        // 4. Coverage: of the gateways that have ever reported, how many have status "online".
        var gateways = allTime.Gateways.Values.ToList();
        var activeGateways = gateways.Count(IsGatewayOnline);
        var gatewaysThen = atPast.Gateways.Count;
        var activeGatewaysThen = atPast.Gateways.Values.Count(IsGatewayOnline);
        var coverage = DashboardMath.Percent(activeGateways, gateways.Count);
        var coverageBefore = DashboardMath.Percent(activeGatewaysThen, gatewaysThen);

        // 5. Data freshness: of the registered assets' tags, how many sent a reading within the SLA.
        var freshTags = assetTags.Count(tag => networkTags.TryGetValue(tag, out var summary) && summary.LastSeenAt >= now.AddMinutes(-DataFreshnessSlaMinutes));
        var freshTagsThen = assetTags.Count(tag => atPast.Tags.TryGetValue(tag, out var seen) && seen.LastSeenAt >= past.AddMinutes(-DataFreshnessSlaMinutes));
        var freshness = DashboardMath.Percent(freshTags, assetTags.Count);
        var freshnessBefore = DashboardMath.Percent(freshTagsThen, assetTags.Count);

        return Results.Ok(new AssetDashboardKpisResponse(
            period.Days,
            period.FromUtc,
            period.ToUtc,
            assets.Count,
            new AssetDashboardKpi(connected.Count, DashboardMath.Percent(connected.Count, assets.Count),
                DashboardMath.Change(connected.Count, connectedBefore), $"of {assets.Count} registered assets"),
            new AssetDashboardKpi(online, DashboardMath.Percent(online, connected.Count), null, "of connected assets"),
            new AssetDashboardKpi(alertsNow, null, DashboardMath.Change(alertsNow, alertsBefore), $"in last {period.Days} days"),
            // No change when no gateway had reported yet at the earlier moment (nothing to compare with).
            new AssetDashboardKpi(coverage, coverage, gatewaysThen == 0 ? null : PointsChange(coverage, coverageBefore),
                $"{activeGateways} of {gateways.Count} gateways with status online; change in percentage points vs. {period.Days} days ago"),
            new AssetDashboardKpi(freshness, freshness, PointsChange(freshness, freshnessBefore),
                $"{freshTags} of {assetTags.Count} asset tags sent data within {DataFreshnessSlaMinutes} min (SLA); change in percentage points vs. {period.Days} days ago"),
            connectedDetails));
    }

    /// <summary>6. Map pins, legend counts and the "Top Active Sites" panel; <paramref name="top"/> = 0 returns every site (View All).</summary>
    private static async Task<IResult> GetLocationsAsync(IAssetDashboardRepository repository, [AsParameters] AssetDashboardFilter filter, CancellationToken cancellationToken, int? top = null)
    {
        if (filter.Invalid() is { } invalidFilter)
        {
            return invalidFilter;
        }

        if (top is < 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["top"] = ["top must be 0 (all) or more"] });
        }

        var networkTags = await repository.GetNetworkTagsAsync(cancellationToken);
        var assets = FilterAssets(await repository.GetAssetsAsync(cancellationToken), filter, networkTags);
        var assetIds = assets.Select(asset => asset.AssetRegistrationId).ToList();
        var devices = await repository.GetDevicesAsync(assetIds, cancellationToken);
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

        // Alert when the asset has an open alert, otherwise its network status (same rule as the Status filter and the sites).
        string PinStatus(DashboardAsset asset) =>
            openAlertAssets.Contains(asset.AssetRegistrationId) ? Alert : NetworkState(NetworkTag(asset, networkTags));

        var pins = assets
            .Where(asset => asset.Latitude is not null && asset.Longitude is not null)
            .Select(asset => new AssetLocationPin(
                asset.AssetRegistrationId, asset.AssetTag, asset.AssetName, asset.AssetType, asset.SiteName,
                asset.Latitude!.Value, asset.Longitude!.Value, PinStatus(asset),
                lastZone.GetValueOrDefault(asset.AssetRegistrationId),
                NetworkTag(asset, networkTags)?.LastSeenAt ?? lastSeen.GetValueOrDefault(asset.AssetRegistrationId)))
            .ToList();

        var legend = new[] { Online, Idle, Offline, Alert, Unknown }
            .ToDictionary(status => status, status => assets.Count(asset => PinStatus(asset) == status));

        // Sites: assets grouped by location.site_id, named from master_sites; status from NetworkTimeSeries (tag id = asset tag).
        // Rank by asset count, highest first, then by assets online.
        var siteMaster = await repository.GetSitesAsync(cancellationToken);
        var sites = assets
            .GroupBy(SiteKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var counts = SiteCounts.From(group, networkTags);
                var (name, code) = SiteNameAndCode(group.Key, group, siteMaster);
                return new AssetSiteActivity(group.Key, name, code, counts.Total, counts.OnlinePercent,
                    counts.Online, counts.Idle, counts.Offline, counts.Unknown);
            })
            .OrderByDescending(site => site.TotalAssets)
            .ThenByDescending(site => site.Online)
            .ThenBy(site => site.SiteName)
            .ToList();

        var shownSites = (top ?? DefaultTopSites) == 0 ? sites : sites.Take(top ?? DefaultTopSites).ToList();
        return Results.Ok(new AssetLocationMapResponse(pins, legend, assets.Count - pins.Count, shownSites, sites.Count));
    }

    /// <summary>
    /// Every site with its linked assets: each site in master_sites plus any site id found on assets but missing there
    /// ("unassigned" for assets without a site). Per site: details, total and Online / Idle / Offline / Unknown counts, and
    /// the assets grouped by network status, as GET /sites/{siteId} returns for one site. siteId, assetType and status
    /// narrow the sites and assets; sites with no matching assets are still listed (count 0) unless siteId is set.
    /// Most assets first, then by name; "unassigned" last.
    /// </summary>
    private static async Task<IResult> GetAllSiteAssetsAsync(
        IAssetDashboardRepository repository, [AsParameters] AssetDashboardFilter filter, CancellationToken cancellationToken)
    {
        if (filter.Invalid() is { } invalidFilter)
        {
            return invalidFilter;
        }

        var siteMaster = await repository.GetSitesAsync(cancellationToken);
        var networkTags = await repository.GetNetworkTagsAsync(cancellationToken);
        var allAssets = await repository.GetAssetsAsync(cancellationToken);
        var assets = FilterAssets(allAssets, filter, networkTags);

        var sites = siteMaster.Keys
            .Concat(allAssets.Select(SiteKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(siteId => filter.Site is null || string.Equals(siteId, filter.Site, StringComparison.OrdinalIgnoreCase))
            .Select(siteId => BuildSiteDetail(siteId, assets, siteMaster, networkTags, allAssets))
            .OfType<AssetSiteDetailResponse>()
            .OrderBy(site => site.SiteId == UnassignedSiteId)
            .ThenByDescending(site => site.TotalAssets)
            .ThenBy(site => site.SiteName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Results.Ok(new AllSitesAssetsResponse(sites.Count, sites.Sum(site => site.TotalAssets), sites));
    }

    /// <summary>
    /// Top Active Sites drill-down: the assets mapped to <paramref name="siteId"/> (location.site_id; "unassigned" for assets
    /// with no site), their count, and each asset's network status from NetworkTimeSeries (tag id = asset tag).
    /// </summary>
    private static async Task<IResult> GetSiteAssetsAsync(string siteId, IAssetDashboardRepository repository, [AsParameters] AssetDashboardFilter filter, CancellationToken cancellationToken)
    {
        if (filter.Invalid() is { } invalidFilter)
        {
            return invalidFilter;
        }

        siteId = siteId.Trim();
        var siteMaster = await repository.GetSitesAsync(cancellationToken);
        var networkTags = await repository.GetNetworkTagsAsync(cancellationToken);
        // The site comes from the route; asset type and status still narrow its assets.
        var assets = FilterAssets(await repository.GetAssetsAsync(cancellationToken),
            new AssetDashboardFilter { AssetType = filter.AssetType, Status = filter.Status }, networkTags);

        var detail = BuildSiteDetail(siteId, assets, siteMaster, networkTags);
        return detail is null
            ? Results.NotFound(new { message = $"Site '{siteId}' was not found." })
            : Results.Ok(detail);
    }

    /// <summary>
    /// One tag: its NetworkTimeSeries details (first / last reading, latest status, gateway, RSSI, reading count, newest
    /// readings) and every asset whose asset tag is this tag id. <paramref name="readings"/> is how many recent readings to
    /// return (default 20, 0 for none, at most 500). 404 when the tag has never reported and no asset uses it.
    /// </summary>
    private static async Task<IResult> GetTagAsync(
        string tagId,
        IAssetDashboardRepository repository,
        CancellationToken cancellationToken,
        int? readings = null)
    {
        var limit = readings ?? DefaultTagReadings;
        if (limit is < 0 or > MaxTagReadings)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["readings"] = [$"readings must be between 0 and {MaxTagReadings}"] });
        }

        tagId = tagId.Trim();
        var tag = await repository.GetNetworkTagAsync(tagId, cancellationToken);
        var assets = await repository.GetAssetsAsync(cancellationToken);
        var mapped = assets
            .Where(asset => string.Equals(asset.AssetTag?.Trim(), tagId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(asset => asset.AssetName, StringComparer.OrdinalIgnoreCase)
            .Select(asset => new TagMappedAsset(asset.AssetRegistrationId, asset.AssetTag, asset.AssetName, asset.AssetType, asset.Status,
                asset.SiteId, asset.SiteName, asset.BuildingName, asset.FloorName, asset.ZoneName, asset.LocationPath,
                asset.Latitude, asset.Longitude))
            .ToList();

        if (tag is null && mapped.Count == 0)
        {
            return Results.NotFound(new { message = $"Tag '{tagId}' has no readings in NetworkTimeSeries and is not mapped to any asset." });
        }

        var recent = tag is null || limit == 0
            ? []
            : (await repository.GetNetworkTagReadingsAsync(tagId, limit, cancellationToken))
                .Select(reading => new TagReading(reading.Timestamp, reading.GatewayId, reading.Status, reading.Rssi, reading.Raw))
                .ToList();
        // Latest status, gateway and RSSI all come from the same (newest) reading.
        var newest = recent.Count > 0
            ? recent[0]
            : tag is null ? null : (await repository.GetNetworkTagReadingsAsync(tagId, 1, cancellationToken))
                .Select(reading => new TagReading(reading.Timestamp, reading.GatewayId, reading.Status, reading.Rssi, reading.Raw))
                .FirstOrDefault();

        return Results.Ok(new TagDetailResponse(
            tag?.TagId ?? tagId,
            mapped.Count > 0,
            mapped.Count > 0 ? NetworkState(tag) : Unknown,
            newest?.Status ?? tag?.LastStatus,
            newest?.GatewayId ?? tag?.LastGatewayId,
            newest?.Rssi,
            tag?.FirstSeenAt,
            tag?.LastSeenAt,
            tag?.ReadingCount ?? 0,
            recent,
            mapped));
    }

    /// <summary>Asset count per category (classification.category) with a sub-category breakdown, highest count first.</summary>
    private static async Task<IResult> GetCategoryCountsAsync(IAssetDashboardRepository repository, CancellationToken cancellationToken)
    {
        var assets = await repository.GetAssetsAsync(cancellationToken);

        var categories = assets
            .GroupBy(asset => NameOr(asset.Category, Uncategorized), StringComparer.OrdinalIgnoreCase)
            .Select(group => new AssetCategoryCount(
                group.First().Category?.Trim() ?? Uncategorized,
                group.Count(),
                DashboardMath.Percent(group.Count(), assets.Count),
                group.GroupBy(asset => NameOr(asset.SubCategory, Uncategorized), StringComparer.OrdinalIgnoreCase)
                    .Select(sub => new AssetSubCategoryCount(sub.Key, sub.Count()))
                    .OrderByDescending(sub => sub.Count)
                    .ThenBy(sub => sub.SubCategory, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .OrderByDescending(category => category.Count)
            .ThenBy(category => category.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Results.Ok(new AssetCategoryCountsResponse(assets.Count, categories));

        static string NameOr(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    /// <summary>Assets whose classification.criticality is "Critical", with their count.</summary>
    private static Task<IResult> GetCriticalAssetsAsync(IAssetDashboardRepository repository, CancellationToken cancellationToken) =>
        AssetCheckAsync(repository, detail => string.Equals(detail.Criticality?.Trim(), CriticalLevel, StringComparison.OrdinalIgnoreCase), cancellationToken);

    /// <summary>Assets with no custodian name (location_assignment.assignment.assigned_to_name empty), with their count.</summary>
    private static Task<IResult> GetAssetsWithoutCustodianAsync(IAssetDashboardRepository repository, CancellationToken cancellationToken) =>
        AssetCheckAsync(repository, detail => detail.CustodianName is null, cancellationToken);

    /// <summary>
    /// Assets with no document anywhere: no purchase / warranty documents, no attachments, and nothing on the
    /// Asset Documents screen (AssetDocument), with their count.
    /// </summary>
    private static Task<IResult> GetAssetsWithoutDocumentsAsync(IAssetDashboardRepository repository, CancellationToken cancellationToken) =>
        AssetCheckAsync(repository, detail => detail.DocumentCount == 0, cancellationToken);

    private static async Task<IResult> AssetCheckAsync(
        IAssetDashboardRepository repository,
        Func<AssetCheckDetail, bool> matches,
        CancellationToken cancellationToken)
    {
        var details = await GetAssetDetailsAsync(repository, cancellationToken);
        var matching = details.Where(matches).ToList();

        return Results.Ok(new AssetCheckResponse(matching.Count, details.Count, matching));
    }

    /// <summary>
    /// Every asset status with its asset count, share of all assets and the assets behind the count. The standard
    /// statuses (Asset Registration's Status dropdown) always come first in their usual order, 0 when unused; any other
    /// value found on assets follows, then "No Status" for assets without one. <paramref name="status"/> narrows it to
    /// one status; <paramref name="includeAssets"/>=false returns the counts only.
    /// </summary>
    private static async Task<IResult> GetStatusCountsAsync(
        IAssetDashboardRepository repository,
        CancellationToken cancellationToken,
        string? status = null,
        bool includeAssets = true)
    {
        var details = await GetAssetDetailsAsync(repository, cancellationToken);
        var byStatus = details
            .GroupBy(detail => StatusOf(detail.AssetStatus), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var standard = AssetRegistrationOptions.Statuses.All;
        var others = byStatus.Keys
            .Where(key => !standard.Contains(key, StringComparer.OrdinalIgnoreCase) && key != NoStatus)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase);
        var noStatus = byStatus.ContainsKey(NoStatus) ? new[] { NoStatus } : [];

        var statuses = standard.Concat(others).Concat(noStatus)
            .Where(name => string.IsNullOrWhiteSpace(status) || string.Equals(name, status.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(name =>
            {
                var assets = byStatus.GetValueOrDefault(name) ?? [];
                return new AssetStatusCount(name, assets.Count, DashboardMath.Percent(assets.Count, details.Count), includeAssets ? assets : []);
            })
            .ToList();

        return Results.Ok(new AssetStatusCountsResponse(details.Count, statuses.Count, statuses));

        // Standard statuses keep their standard spelling ("active" counts as "Active")
        static string StatusOf(string? value) =>
            string.IsNullOrWhiteSpace(value) ? NoStatus : AssetRegistrationOptions.Statuses.Normalize(value) ?? value.Trim();
    }

    /// <summary>
    /// The summary cards on the asset detail page: components, relationships, documents, images, work orders,
    /// service requests and open alerts for one asset. <paramref name="assetRegistrationId"/> may also be the asset tag.
    /// </summary>
    private static async Task<IResult> GetAssetSummaryCountsAsync(
        string assetRegistrationId,
        IAssetDashboardRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetRegistrationId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["AssetRegistrationId"] = ["Asset registration id is required"] });
        }

        var counts = await repository.GetAssetSummaryCountsAsync(assetRegistrationId, cancellationToken);
        return counts is null
            ? Results.NotFound(new { message = $"Asset registration '{assetRegistrationId.Trim()}' was not found." })
            : Results.Ok(new AssetSummaryCountsResponse(
                counts.AssetRegistrationId, counts.AssetTag, counts.AssetName, counts.Components, counts.Relationships,
                counts.Documents, counts.Images, counts.WorkOrders, counts.ServiceRequests, counts.OpenAlerts));
    }

    /// <summary>All registered assets as dashboard detail rows, sorted by name.</summary>
    private static async Task<List<AssetCheckDetail>> GetAssetDetailsAsync(IAssetDashboardRepository repository, CancellationToken cancellationToken)
    {
        var assets = await repository.GetAssetsAsync(cancellationToken);
        var documentRecords = await repository.CountDocumentRecordsByAssetAsync(cancellationToken);

        return assets
            .Select(asset => new AssetCheckDetail(asset.AssetRegistrationId, asset.AssetTag, asset.AssetName, asset.Category, asset.SubCategory,
                asset.AssetType, asset.Criticality, asset.Status, asset.SiteId, asset.SiteName, asset.LocationPath,
                string.IsNullOrWhiteSpace(asset.CustodianName) ? null : asset.CustodianName.Trim(), asset.CustodianDepartment,
                asset.AttachedDocumentCount + documentRecords.GetValueOrDefault(asset.AssetRegistrationId)))
            .OrderBy(detail => detail.AssetName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(detail => detail.AssetRegistrationId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>One site's counts and assets; null when the id is neither in master_sites nor on any asset.</summary>
    private static AssetSiteDetailResponse? BuildSiteDetail(
        string siteId,
        IReadOnlyList<DashboardAsset> assets,
        IReadOnlyDictionary<string, DashboardSite> siteMaster,
        IReadOnlyDictionary<string, NetworkTagSummary> networkTags,
        IReadOnlyList<DashboardAsset>? unfilteredAssets = null)
    {
        var siteAssets = assets
            .Where(asset => string.Equals(SiteKey(asset), siteId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        // When filters narrowed the assets, the site's unfiltered assets still name a site that is not in master data.
        var namingAssets = unfilteredAssets is null
            ? siteAssets
            : unfilteredAssets.Where(asset => string.Equals(SiteKey(asset), siteId, StringComparison.OrdinalIgnoreCase)).ToList();

        siteMaster.TryGetValue(siteId, out var site);
        if (site is null && namingAssets.Count == 0)
        {
            return null;
        }

        var counts = SiteCounts.From(siteAssets, networkTags);
        var (name, code) = SiteNameAndCode(siteId, namingAssets, siteMaster);

        var details = siteAssets
            .Select(asset =>
            {
                var tag = NetworkTag(asset, networkTags);
                return new SiteAssetDetail(asset.AssetRegistrationId, asset.AssetTag, asset.AssetName, asset.AssetType, asset.Status,
                    asset.BuildingName, asset.FloorName, asset.ZoneName, asset.LocationPath, asset.Latitude, asset.Longitude,
                    NetworkState(tag), tag?.LastGatewayId, tag?.LastSeenAt);
            })
            .ToList();

        // One group per status, highest count first; each group carries the assets behind its count.
        var statusOrder = new[] { Online, Idle, Offline, Unknown };
        var statuses = statusOrder
            .Select(status => details
                .Where(detail => detail.NetworkStatus == status)
                .OrderBy(detail => detail.AssetName, StringComparer.OrdinalIgnoreCase)
                .ToList())
            .Select((group, index) => new SiteStatusGroup(statusOrder[index], group.Count, group))
            .OrderByDescending(group => group.Count)
            .ThenBy(group => Array.IndexOf(statusOrder, group.Status))
            .ToList();

        return new AssetSiteDetailResponse(site?.Id ?? siteId, name, code, site?.SiteType, site?.City, site?.Country,
            counts.Total, counts.OnlinePercent, statuses);
    }

    /// <summary>
    /// 7. Device connectivity of every tag in NetworkTimeSeries (meta.tagid). A tag is mapped when it equals a registered
    /// asset's tag (basic_information.asset_tag). Mapped tags: Idle when the newest reading is older than
    /// <see cref="IdleAfterMinutes"/> minutes, otherwise Online or Offline from that reading's status. Tags not mapped to
    /// any asset are Unknown.
    /// </summary>
    private static async Task<IResult> GetDeviceConnectivityAsync(IAssetDashboardRepository repository, [AsParameters] AssetDashboardFilter filter, CancellationToken cancellationToken)
    {
        if (filter.Invalid() is { } invalidFilter)
        {
            return invalidFilter;
        }

        var networkTags = await repository.GetNetworkTagsAsync(cancellationToken);
        var allAssets = await repository.GetAssetsAsync(cancellationToken);
        var allAssetTags = TagsOf(allAssets);
        var assetTags = TagsOf(FilterAssets(allAssets, filter, networkTags));

        // Tags not mapped to any asset have no site or type: they count (as Unknown) only when no site / type is chosen
        // and the status is All or Unknown.
        var includeUnmapped = filter.Site is null && filter.Type is null && filter.State is null or Unknown;
        var statuses = networkTags.Values
            .Where(tag => assetTags.Contains(tag.TagId) || (includeUnmapped && !allAssetTags.Contains(tag.TagId)))
            .Select(tag => assetTags.Contains(tag.TagId) ? NetworkState(tag) : Unknown)
            .ToList();

        return Results.Ok(Donut(statuses, Online, [Online, Idle, Offline, Unknown]));
    }

    /// <summary>8. Share of devices on registered assets that run the latest firmware released for their model.</summary>
    private static async Task<IResult> GetFirmwareAsync(IAssetDashboardRepository repository, [AsParameters] AssetDashboardFilter filter, CancellationToken cancellationToken)
    {
        if (filter.Invalid() is { } invalidFilter)
        {
            return invalidFilter;
        }

        var assets = await FilteredAssetsAsync(repository, filter, cancellationToken);
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
    private static async Task<IResult> GetHealthScoresAsync(IAssetDashboardRepository repository, [AsParameters] AssetDashboardFilter filter, CancellationToken cancellationToken)
    {
        if (filter.Invalid() is { } invalidFilter)
        {
            return invalidFilter;
        }

        var assets = await FilteredAssetsAsync(repository, filter, cancellationToken);
        var scores = await repository.GetLatestHealthScoresAsync(cancellationToken);

        var bands = assets.Select(asset => scores.TryGetValue(asset.AssetRegistrationId, out var score)
                ? score >= 70 ? "Healthy" : score >= 40 ? "Warning" : "Critical"
                : Unknown)
            .ToList();

        return Results.Ok(Donut(bands, "Healthy", ["Healthy", "Warning", "Critical", Unknown]));
    }

    /// <summary>10. Running % vs. idle % per asset type over the last <paramref name="days"/> days (default 7); type comes from the asset registration.</summary>
    private static async Task<IResult> GetUtilizationAsync(IAssetDashboardRepository repository, [AsParameters] AssetDashboardFilter filter, CancellationToken cancellationToken, int? days = null)
    {
        if (filter.Invalid() is { } invalidFilter)
        {
            return invalidFilter;
        }

        if (AssetDashboardPeriod.Invalid(days) is { } invalidDays)
        {
            return invalidDays;
        }

        var period = AssetDashboardPeriod.LastDays(days);
        var assets = await FilteredAssetsAsync(repository, filter, cancellationToken);
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
    private static async Task<IResult> GetEventsTimelineAsync(IAssetDashboardRepository repository, [AsParameters] AssetDashboardFilter filter, CancellationToken cancellationToken, int? days = null)
    {
        if (filter.Invalid() is { } invalidFilter)
        {
            return invalidFilter;
        }

        if (AssetDashboardPeriod.Invalid(days) is { } invalidDays)
        {
            return invalidDays;
        }

        var period = AssetDashboardPeriod.LastDays(days);
        var assets = await FilteredAssetsAsync(repository, filter, cancellationToken);
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

    /// <summary>
    /// Location updates: per tag in NetworkTimeSeries, each time a reading arrives from a different gateway than the tag's
    /// previous reading counts once (A → B → A is 2). Covers every tag that reports, registered or not. Without
    /// <paramref name="days"/> all readings count; with it, changes in the last N days. <paramref name="tagId"/> narrows to one tag.
    /// </summary>
    private static async Task<IResult> GetLocationUpdatesAsync(
        IAssetDashboardRepository repository,
        [AsParameters] AssetDashboardFilter filter,
        CancellationToken cancellationToken,
        int? days = null,
        string? tagId = null)
    {
        if (AssetDashboardPeriod.Invalid(days) is { } invalidDays)
        {
            return invalidDays;
        }

        if (filter.Invalid() is { } invalidFilter)
        {
            return invalidFilter;
        }

        // Without filters every tag counts; with them, only the tags of the matching registered assets.
        IReadOnlyCollection<string>? tagIds = filter.IsSet
            ? TagsOf(await FilteredAssetsAsync(repository, filter, cancellationToken))
            : null;
        if (!string.IsNullOrWhiteSpace(tagId))
        {
            tagIds = tagIds is null || tagIds.Contains(tagId.Trim(), StringComparer.OrdinalIgnoreCase) ? [tagId.Trim()] : [];
        }

        var period = days is null ? null : AssetDashboardPeriod.LastDays(days);
        var changes = await repository.CountGatewayChangesAsync(period?.FromUtc, period?.ToUtc, tagIds, cancellationToken);

        var tags = changes
            .Select(tag => new TagLocationUpdates(tag.TagId, tag.Changes, tag.LastChangedAt, tag.LastFromGatewayId, tag.LastToGatewayId,
                tag.LastFromLatitude, tag.LastFromLongitude, tag.LastToLatitude, tag.LastToLongitude))
            .ToList();

        return Results.Ok(new AssetLocationUpdatesResponse(period?.Days, period?.FromUtc, period?.ToUtc,
            tags.Sum(tag => tag.Count), tags.Count, tags));
    }

    /// <summary>12. Sensors on registered assets whose status is Attention (abnormal reading).</summary>
    private static async Task<IResult> GetSensorsAttentionAsync(IAssetDashboardRepository repository, [AsParameters] AssetDashboardFilter filter, CancellationToken cancellationToken)
    {
        if (filter.Invalid() is { } invalidFilter)
        {
            return invalidFilter;
        }

        var assets = await FilteredAssetsAsync(repository, filter, cancellationToken);
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
        [AsParameters] AssetDashboardFilter filter,
        CancellationToken cancellationToken,
        int? days = null,
        string? alertStatus = null,
        string? severity = null,
        int? limit = null)
    {
        if (filter.Invalid() is { } invalidFilter)
        {
            return invalidFilter;
        }

        if (AssetDashboardPeriod.Invalid(days) is { } invalidDays)
        {
            return invalidDays;
        }

        if (limit is < 1)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["limit"] = ["limit must be at least 1"] });
        }

        var assets = (await FilteredAssetsAsync(repository, filter, cancellationToken))
            .ToDictionary(asset => asset.AssetRegistrationId, StringComparer.OrdinalIgnoreCase);
        var period = days is null ? null : AssetDashboardPeriod.LastDays(days);
        var alerts = await repository.GetAlertsAsync(assets.Keys.ToList(), period?.FromUtc, period?.ToUtc, cancellationToken);

        return Results.Ok(alerts
            .Where(alert => string.IsNullOrWhiteSpace(alertStatus) || string.Equals(alert.Status, alertStatus.Trim(), StringComparison.OrdinalIgnoreCase))
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


    /// <summary>
    /// Values for the Days, Site, Asset Type and Status dropdowns. Sites: every site in master_sites plus any site id found
    /// on assets but missing there ("unassigned" for assets without a site), with asset counts. Asset types: the types
    /// used on registered assets. Statuses: the network statuses the Status filter accepts.
    /// </summary>
    private static async Task<IResult> GetFiltersAsync(IAssetDashboardRepository repository, CancellationToken cancellationToken)
    {
        var assets = await repository.GetAssetsAsync(cancellationToken);
        var siteMaster = await repository.GetSitesAsync(cancellationToken);

        var assetsBySite = assets.ToLookup(SiteKey, StringComparer.OrdinalIgnoreCase);
        var sites = siteMaster.Keys
            .Concat(assetsBySite.Select(group => group.Key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(siteId =>
            {
                var siteAssets = assetsBySite[siteId].ToList();
                var (name, code) = SiteNameAndCode(siteId, siteAssets, siteMaster);
                return new DashboardSiteOption(siteId, name, code, siteAssets.Count);
            })
            .OrderBy(site => site.SiteId == UnassignedSiteId)
            .ThenBy(site => site.SiteName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var assetTypes = assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.AssetType))
            .GroupBy(asset => asset.AssetType!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new DashboardAssetTypeOption(group.Key, group.Count()))
            .OrderBy(type => type.AssetType, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Results.Ok(new AssetDashboardFiltersResponse([7, 30, 90], sites, assetTypes, AssetDashboardFilter.Statuses));
    }

    /// <summary>Registered assets narrowed by the dashboard filters; network readings are only read when Status is set.</summary>
    private static async Task<IReadOnlyList<DashboardAsset>> FilteredAssetsAsync(
        IAssetDashboardRepository repository, AssetDashboardFilter filter, CancellationToken cancellationToken)
    {
        var assets = await repository.GetAssetsAsync(cancellationToken);
        if (!filter.IsSet)
        {
            return assets;
        }

        var networkTags = filter.State is null ? null : await repository.GetNetworkTagsAsync(cancellationToken);
        return FilterAssets(assets, filter, networkTags);
    }

    /// <summary>
    /// Assets on the chosen site (location.site_id, "unassigned" for none), of the chosen type (classification.asset_type)
    /// and with the chosen network status; each comparison ignores case. <paramref name="networkTags"/> is needed for Status.
    /// </summary>
    private static IReadOnlyList<DashboardAsset> FilterAssets(
        IReadOnlyList<DashboardAsset> assets, AssetDashboardFilter filter, IReadOnlyDictionary<string, NetworkTagSummary>? networkTags)
    {
        if (!filter.IsSet)
        {
            return assets;
        }

        if (filter.State is not null && networkTags is null)
        {
            throw new ArgumentNullException(nameof(networkTags), "Network tags are needed to filter by status.");
        }

        return assets
            .Where(asset => filter.Site is null || string.Equals(SiteKey(asset), filter.Site, StringComparison.OrdinalIgnoreCase))
            .Where(asset => filter.Type is null || string.Equals(asset.AssetType?.Trim(), filter.Type, StringComparison.OrdinalIgnoreCase))
            .Where(asset => filter.State is null || NetworkState(NetworkTag(asset, networkTags!)) == filter.State)
            .ToList();
    }

    private static HashSet<string> TagsOf(IEnumerable<DashboardAsset> assets) =>
        assets
            .Where(asset => !string.IsNullOrWhiteSpace(asset.AssetTag))
            .Select(asset => asset.AssetTag!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The site an asset is mapped to: location.site_id, or "unassigned" when it has none.</summary>
    private static string SiteKey(DashboardAsset asset) =>
        string.IsNullOrWhiteSpace(asset.SiteId) ? UnassignedSiteId : asset.SiteId.Trim();

    /// <summary>Name and code from master_sites; falls back to the name stored on the assets when the id is not in master data.</summary>
    private static (string Name, string? Code) SiteNameAndCode(
        string siteId, IEnumerable<DashboardAsset> siteAssets, IReadOnlyDictionary<string, DashboardSite> siteMaster)
    {
        if (siteMaster.TryGetValue(siteId, out var site) && !string.IsNullOrWhiteSpace(site.SiteName))
        {
            return (site.SiteName.Trim(), site.Code);
        }

        var storedName = siteAssets.Select(asset => asset.SiteName?.Trim()).FirstOrDefault(name => !string.IsNullOrEmpty(name));
        return (storedName ?? "Unassigned", null);
    }

    private static NetworkTagSummary? NetworkTag(DashboardAsset asset, IReadOnlyDictionary<string, NetworkTagSummary> networkTags) =>
        !string.IsNullOrWhiteSpace(asset.AssetTag) && networkTags.TryGetValue(asset.AssetTag.Trim(), out var tag) ? tag : null;

    /// <summary>
    /// Connectivity of a tag mapped to an asset, from NetworkTimeSeries: Unknown when the tag has no readings; Idle when the
    /// newest reading is older than <see cref="IdleAfterMinutes"/> minutes; otherwise Online when its status is "online",
    /// else Offline. Used by Device Connectivity, Top Active Sites and the site endpoints so they always agree.
    /// </summary>
    private static string NetworkState(NetworkTagSummary? tag)
    {
        if (tag is null)
        {
            return Unknown;
        }

        if (tag.LastSeenAt is null || tag.LastSeenAt < DateTime.UtcNow.AddMinutes(-IdleAfterMinutes))
        {
            return Idle;
        }

        return tag.LastStatus == "online" ? Online : Offline;
    }

    /// <summary>A gateway is online when the status field of its newest reading in NetworkTimeSeries is "online".</summary>
    private static bool IsGatewayOnline(NetworkSeen gateway) => gateway.LastStatus == "online";

    private sealed record SiteCounts(int Total, int Online, int Idle, int Offline, int Unknown)
    {
        public double OnlinePercent => DashboardMath.Percent(Online, Total);

        public static SiteCounts From(IEnumerable<DashboardAsset> siteAssets, IReadOnlyDictionary<string, NetworkTagSummary> networkTags)
        {
            var states = siteAssets.Select(asset => NetworkState(NetworkTag(asset, networkTags))).ToList();
            return new SiteCounts(states.Count,
                states.Count(s => s == AssetDashboardEndpoints.Online),
                states.Count(s => s == AssetDashboardEndpoints.Idle),
                states.Count(s => s == AssetDashboardEndpoints.Offline),
                states.Count(s => s == AssetDashboardEndpoints.Unknown));
        }
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

    /// <summary>Difference in percentage points, 1 decimal; null when both are 0 (nothing to compare).</summary>
    private static double? PointsChange(double current, double previous) =>
        current == 0 && previous == 0 ? null : Math.Round(current - previous, 1);

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
