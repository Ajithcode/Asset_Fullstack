namespace A360.Asset.Repository.Dashboard;

/// <summary>
/// Reads for the Asset dashboard: registered assets plus the tracking records (devices, health, firmware, sensors,
/// location events, alerts, utilization, telemetry) that belong to them. Read-only; nothing here writes.
/// </summary>
public interface IAssetDashboardRepository
{
    /// <summary>Every registered (not deleted) asset.</summary>
    Task<IReadOnlyList<DashboardAsset>> GetAssetsAsync(CancellationToken cancellationToken = default);

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
}
