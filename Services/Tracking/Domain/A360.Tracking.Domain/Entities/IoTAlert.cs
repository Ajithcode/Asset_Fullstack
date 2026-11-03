using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Tracking.Domain.Entities;

/// <summary>
/// IoT Tracking Dashboard → Alerts / Recent Alerts &amp; Exceptions: one raised alert on a tracked asset
/// (e.g. AL000001, DG-250 at Dubai HQ, "Geofence Breach", Critical).
/// </summary>
/// <remarks>
/// Alerts are a log: an alert rule or a device can raise many. Status is kept in BaseEntity.Status and is one of
/// <see cref="IoTAlertStatus.All"/>.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class IoTAlert : BaseEntity
{
    [BsonElement("alert_id")]
    public string AlertId { get; set; } = string.Empty;

    [BsonElement("raised_at")]
    public DateTime RaisedAt { get; set; }

    [BsonElement("asset_registration_id")]
    public string? AssetRegistrationId { get; set; }

    [BsonElement("asset_name")]
    public string AssetName { get; set; } = string.Empty;

    [BsonElement("device_id")]
    public string? DeviceId { get; set; }

    [BsonElement("site_id")]
    public string? SiteId { get; set; }

    [BsonElement("site_name")]
    public string? SiteName { get; set; }

    /// <summary>e.g. Geofence Breach, Low Battery, Device Offline, Abnormal Reading.</summary>
    [BsonElement("event_type")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>One of <see cref="IoTAlertSeverity.All"/>.</summary>
    [BsonElement("severity")]
    public string Severity { get; set; } = string.Empty;

    [BsonElement("message")]
    public string? Message { get; set; }

    /// <summary>The alert rule that raised it, when one did.</summary>
    [BsonElement("alert_rule_id")]
    public string? AlertRuleId { get; set; }

    [BsonElement("acknowledged_by")]
    public string? AcknowledgedBy { get; set; }

    [BsonElement("acknowledged_at")]
    public DateTime? AcknowledgedAt { get; set; }

    [BsonElement("resolved_by")]
    public string? ResolvedBy { get; set; }

    [BsonElement("resolved_at")]
    public DateTime? ResolvedAt { get; set; }
}

public static class IoTAlertSeverity
{
    public const string Critical = "Critical";
    public const string Warning = "Warning";
    public const string Info = "Info";

    public static readonly IReadOnlyList<string> All = [Critical, Warning, Info];
}

public static class IoTAlertStatus
{
    public const string Open = "Open";
    public const string Acknowledged = "Acknowledged";
    public const string InProgress = "In Progress";
    public const string Resolved = "Resolved";

    public static readonly IReadOnlyList<string> All = [Open, Acknowledged, InProgress, Resolved];
}

public static class IoTAlertEventType
{
    /// <summary>Suggested event types for the dropdown; other values are accepted.</summary>
    public static readonly IReadOnlyList<string> Suggested =
    [
        "Geofence Breach", "Dwell Exceeded", "Device Offline", "Low Battery", "Abnormal Reading",
        "Signal Lost", "Tamper Detected", "Firmware Update Failed"
    ];
}
