namespace A360.Asset.Repository.Dashboard;

/// <summary>
/// Reads for the Asset dashboard: registered assets plus the tracking records (devices, health, firmware, sensors,
/// location events, alerts, utilization, telemetry) that belong to them. Read-only; nothing here writes.
/// </summary>
public interface IAssetDashboardRepository
{
    /// <summary>Every registered (not deleted) asset.</summary>
    Task<IReadOnlyList<DashboardAsset>> GetAssetsAsync(CancellationToken cancellationToken = default);

    /// <summary>Every site in master data (master_sites, not deleted), keyed by id.</summary>
    Task<IReadOnlyDictionary<string, DashboardSite>> GetSitesAsync(CancellationToken cancellationToken = default);

    /// <summary>Documents filed on the Asset Documents screen (AssetDocument, not deleted), counted per asset registration id.</summary>
    Task<IReadOnlyDictionary<string, int>> CountDocumentRecordsByAssetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Every tag id that has reported to the network (NetworkTimeSeries meta.tagid), keyed case-insensitively,
    /// with its first and last reading, latest status and gateway.
    /// </summary>
    Task<IReadOnlyDictionary<string, NetworkTagSummary>> GetNetworkTagsAsync(CancellationToken cancellationToken = default);

    /// <summary>One tag's summary from NetworkTimeSeries (tag id matched ignoring case); null when it has never reported.</summary>
    Task<NetworkTagSummary?> GetNetworkTagAsync(string tagId, CancellationToken cancellationToken = default);

    /// <summary>One tag's newest readings from NetworkTimeSeries, newest first.</summary>
    Task<IReadOnlyList<NetworkTagReading>> GetNetworkTagReadingsAsync(string tagId, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gateway changes per tag from NetworkTimeSeries: walking each tag's readings in time order, every reading whose
    /// gateway differs from the tag's previous reading counts once. Only changes whose new reading falls in
    /// [<paramref name="fromUtc"/>, <paramref name="toUtc"/>) count (either bound optional); the previous reading may be older.
    /// Tags and gateways are compared ignoring case and spacing. <paramref name="tagIds"/> limits it to those tags (null for
    /// every tag). Tags with no change are absent; most changes first.
    /// </summary>
    Task<IReadOnlyList<NetworkTagGatewayChanges>> CountGatewayChangesAsync(DateTime? fromUtc, DateTime? toUtc, IReadOnlyCollection<string>? tagIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// First and last reading, and the newest reading's status, per tag (meta.tagid) and per gateway (meta.gatewayid) in
    /// NetworkTimeSeries, keyed ignoring case
    /// and spacing. Only readings in [<paramref name="fromUtc"/>, <paramref name="toUtc"/>) count (either bound optional);
    /// <paramref name="tagIds"/> limits it to readings of those tags (null for every tag).
    /// </summary>
    Task<NetworkActivity> GetNetworkActivityAsync(DateTime? fromUtc, DateTime? toUtc, IReadOnlyCollection<string>? tagIds, CancellationToken cancellationToken = default);

    /// <summary>Devices assigned to one of <paramref name="assetRegistrationIds"/>.</summary>
    Task<IReadOnlyList<DashboardDevice>> GetDevicesAsync(IReadOnlyCollection<string> assetRegistrationIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DashboardDeviceHealth>> GetDeviceHealthAsync(IReadOnlyCollection<string> deviceIds, CancellationToken cancellationToken = default);

    /// <summary>The newest firmware record per device model (case-insensitive model key).</summary>
    Task<IReadOnlyDictionary<string, DashboardFirmware>> GetLatestFirmwareByModelAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DashboardSensor>> GetSensorsAsync(IReadOnlyCollection<string> assetRegistrationIds, string? status, CancellationToken cancellationToken = default);

    /// <summary>Location events at or after <paramref name="fromUtc"/> whose asset id is one of <paramref name="assetKeys"/> (registration ids and tags).</summary>
    Task<IReadOnlyList<DashboardLocationEvent>> GetLocationEventsAsync(IReadOnlyCollection<string> assetKeys, DateTime fromUtc, CancellationToken cancellationToken = default);

    /// <summary>Alerts on the given assets, most recent first; the window is optional.</summary>
    Task<IReadOnlyList<DashboardAlert>> GetAlertsAsync(IReadOnlyCollection<string> assetRegistrationIds, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DashboardUtilization>> GetUtilizationAsync(IReadOnlyCollection<string> assetRegistrationIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>Latest condition-assessment health score (0–100) per asset; assets never assessed are absent.</summary>
    Task<IReadOnlyDictionary<string, decimal>> GetLatestHealthScoresAsync(CancellationToken cancellationToken = default);

    /// <summary>Telemetry readings per UTC day in [from, to) on the given assets.</summary>
    Task<IReadOnlyList<DashboardDailyCount>> CountTelemetryPerDayAsync(IReadOnlyCollection<string> assetRegistrationIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>Alerts raised per UTC day in [from, to) on the given assets.</summary>
    Task<IReadOnlyList<DashboardDailyCount>> CountAlertsPerDayAsync(IReadOnlyCollection<string> assetRegistrationIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>Location events per UTC day and event type in [from, to) on the given asset keys (registration ids and tags).</summary>
    Task<IReadOnlyList<DashboardDailyCount>> CountLocationEventsPerDayAsync(IReadOnlyCollection<string> assetKeys, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts of components, relationships, documents, images, work orders, service requests and open alerts for one
    /// asset, found by registration id or asset tag (ignoring case); null when no such asset is registered.
    /// </summary>
    Task<AssetSummaryCounts?> GetAssetSummaryCountsAsync(string assetRegistrationIdOrTag, CancellationToken cancellationToken = default);
}
