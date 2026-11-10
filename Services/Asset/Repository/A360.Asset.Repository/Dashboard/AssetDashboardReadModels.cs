using MongoDB.Bson.Serialization.Attributes;

namespace A360.Asset.Repository.Dashboard;

// Read-only views of the records the Asset dashboard needs. The Tracking service owns and writes these collections
// in the shared Asset360 database; the Asset service only reads the few fields below, so it does not depend on the
// Tracking projects and nothing is copied.

/// <summary>A registered asset with the fields the dashboard shows.</summary>
public sealed record DashboardAsset(
    string AssetRegistrationId,
    string? AssetTag,
    string? AssetName,
    string? Status,
    string? AssetType,
    string? SiteName,
    double? Latitude,
    double? Longitude,
    string? SiteId = null,
    string? LocationPath = null,
    string? BuildingName = null,
    string? FloorName = null,
    string? ZoneName = null,
    string? Category = null,
    string? SubCategory = null,
    string? Criticality = null,
    string? CustodianName = null,
    string? CustodianDepartment = null,
    int AttachedDocumentCount = 0);

/// <summary>Tracking IoTDevice: a connected device and the asset it is assigned to.</summary>
[BsonIgnoreExtraElements]
public sealed class DashboardDevice
{
    [BsonElement("device_id")] public string DeviceId { get; set; } = string.Empty;
    [BsonElement("device_name")] public string? DeviceName { get; set; }
    [BsonElement("model")] public string? Model { get; set; }
    /// <summary>Online, Offline or Warning.</summary>
    [BsonElement("connectivity")] public string? Connectivity { get; set; }
    [BsonElement("asset_registration_id")] public string? AssetRegistrationId { get; set; }
    [BsonElement("last_seen_at")] public DateTime? LastSeenAt { get; set; }
    [BsonElement("firmware_version")] public string? FirmwareVersion { get; set; }
    [BsonElement("created_at")] public DateTime? CreatedAt { get; set; }
    [BsonElement("is_deleted")] public bool IsDeleted { get; set; }
}

/// <summary>Tracking DeviceHealth: the latest connectivity of one device.</summary>
[BsonIgnoreExtraElements]
public sealed class DashboardDeviceHealth
{
    [BsonElement("device_id")] public string DeviceId { get; set; } = string.Empty;
    [BsonElement("asset_registration_id")] public string? AssetRegistrationId { get; set; }
    [BsonElement("connectivity")] public string? Connectivity { get; set; }
    [BsonElement("is_deleted")] public bool IsDeleted { get; set; }
}

/// <summary>Tracking FirmwareVersion: the latest firmware released for a device model.</summary>
[BsonIgnoreExtraElements]
public sealed class DashboardFirmware
{
    [BsonElement("device_model")] public string DeviceModel { get; set; } = string.Empty;
    [BsonElement("latest_version")] public string LatestVersion { get; set; } = string.Empty;
    [BsonElement("release_date")] public DateTime? ReleaseDate { get; set; }
    [BsonElement("is_deleted")] public bool IsDeleted { get; set; }
}

/// <summary>Tracking Sensor: one measurement point on a device.</summary>
[BsonIgnoreExtraElements]
public sealed class DashboardSensor
{
    [BsonElement("sensor_id")] public string SensorId { get; set; } = string.Empty;
    [BsonElement("name")] public string? Name { get; set; }
    [BsonElement("sensor_type")] public string? SensorType { get; set; }
    [BsonElement("measured_parameter")] public string? MeasuredParameter { get; set; }
    [BsonElement("unit")] public string? Unit { get; set; }
    [BsonElement("live_reading")] public decimal? LiveReading { get; set; }
    [BsonElement("device_id")] public string? DeviceId { get; set; }
    [BsonElement("asset_registration_id")] public string? AssetRegistrationId { get; set; }
    /// <summary>Active, Offline, Attention or Inactive.</summary>
    [BsonElement("status")] public string? Status { get; set; }
    [BsonElement("is_deleted")] public bool IsDeleted { get; set; }
}

/// <summary>Tracking LocationEvent: one zone event; <see cref="AssetId"/> is the asset id or tag shown on the map.</summary>
[BsonIgnoreExtraElements]
public sealed class DashboardLocationEvent
{
    [BsonElement("asset_id")] public string AssetId { get; set; } = string.Empty;
    [BsonElement("event_time")] public DateTime EventTime { get; set; }
    [BsonElement("event_type")] public string? EventType { get; set; }
    [BsonElement("zone_name")] public string? ZoneName { get; set; }
    [BsonElement("is_deleted")] public bool IsDeleted { get; set; }
}

/// <summary>Tracking IoTAlert: one raised alert.</summary>
[BsonIgnoreExtraElements]
public sealed class DashboardAlert
{
    [BsonElement("alert_id")] public string AlertId { get; set; } = string.Empty;
    [BsonElement("raised_at")] public DateTime RaisedAt { get; set; }
    [BsonElement("asset_registration_id")] public string? AssetRegistrationId { get; set; }
    [BsonElement("asset_name")] public string? AssetName { get; set; }
    [BsonElement("device_id")] public string? DeviceId { get; set; }
    [BsonElement("site_name")] public string? SiteName { get; set; }
    [BsonElement("event_type")] public string? EventType { get; set; }
    /// <summary>Critical, Warning or Info.</summary>
    [BsonElement("severity")] public string? Severity { get; set; }
    /// <summary>Open, Acknowledged, In Progress or Resolved.</summary>
    [BsonElement("status")] public string? Status { get; set; }
    [BsonElement("message")] public string? Message { get; set; }
    [BsonElement("is_deleted")] public bool IsDeleted { get; set; }
}

/// <summary>Tracking AssetUtilization: running / idle minutes of one asset on one day.</summary>
[BsonIgnoreExtraElements]
public sealed class DashboardUtilization
{
    [BsonElement("asset_registration_id")] public string AssetRegistrationId { get; set; } = string.Empty;
    [BsonElement("asset_type")] public string? AssetType { get; set; }
    [BsonElement("date")] public DateTime Date { get; set; }
    [BsonElement("running_minutes")] public int RunningMinutes { get; set; }
    [BsonElement("idle_minutes")] public int IdleMinutes { get; set; }
    [BsonElement("is_deleted")] public bool IsDeleted { get; set; }
}

/// <summary>One bucket of a per-day count: the UTC day (yyyy-MM-dd), an optional group key and the number of records.</summary>
public sealed record DashboardDailyCount(string Date, string? Key, int Count);

/// <summary>One tag in NetworkTimeSeries, summarised over all of its readings.</summary>
/// <param name="LastStatus">Status of the newest reading, lower-cased (e.g. "online", "offline").</param>
/// <param name="LastGatewayId">Gateway of the newest reading (meta.gatewayid).</param>
public sealed record NetworkTagSummary(
    string TagId,
    DateTime? FirstSeenAt,
    DateTime? LastSeenAt,
    string? LastStatus,
    string? LastGatewayId,
    long ReadingCount);

/// <summary>One NetworkTimeSeries reading of a tag.</summary>
public sealed record NetworkTagReading(DateTime? Timestamp, string? GatewayId, string? Status, double? Rssi, string? Raw);

/// <summary>A site from master data (master_sites), owned by the MasterDataManagement service.</summary>
[BsonIgnoreExtraElements]
public sealed class DashboardSite
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("site_name")] public string? SiteName { get; set; }
    [BsonElement("code")] public string? Code { get; set; }
    [BsonElement("site_type")] public string? SiteType { get; set; }
    [BsonElement("city")] public string? City { get; set; }
    [BsonElement("country")] public string? Country { get; set; }
    [BsonElement("status")] public string? Status { get; set; }
    [BsonElement("is_deleted")] public bool IsDeleted { get; set; }
}

/// <summary>Counts of the records linked to one asset, for the summary cards on the asset detail page.</summary>
/// <param name="Relationships">Related-asset links where the asset is the main asset (one per sub asset) or a sub asset.</param>
/// <param name="Documents">Asset Documents records plus files attached on the registration (warranty documents, attachments).</param>
/// <param name="WorkOrders">Maintenance work orders whose asset_id is the asset's registration id or tag.</param>
/// <param name="OpenAlerts">IoT alerts on the asset that are not Resolved.</param>
public sealed record AssetSummaryCounts(
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
