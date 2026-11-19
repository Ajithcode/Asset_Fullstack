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

    /// <summary>Geofence id, or "zone:{id}" for a floor plan zone, when a geofence rule raised the alert.</summary>
    [BsonElement("geofence_id")]
    public string? GeofenceId { get; set; }

    [BsonElement("geofence_name")]
    public string? GeofenceName { get; set; }

    /// <summary>The raising rule's severity (Informational..Critical); <see cref="Severity"/> keeps the 3-level dashboard value.</summary>
    [BsonElement("rule_severity")]
    public string? RuleSeverity { get; set; }

    /// <summary>Dwell time / stay length the event refers to, in seconds.</summary>
    [BsonElement("duration_seconds")]
    public int? DurationSeconds { get; set; }

    /// <summary>User or team the alert is assigned to.</summary>
    [BsonElement("assigned_to")]
    public string? AssignedTo { get; set; }

    [BsonElement("escalated_at")]
    public DateTime? EscalatedAt { get; set; }

    [BsonElement("escalated_to")]
    public string? EscalatedTo { get; set; }

    /// <summary>Audit trail: raised, acknowledged, assigned, escalated, resolved, closed…</summary>
    [BsonElement("history")]
    public List<IoTAlertHistoryEntry> History { get; set; } = [];
}

public sealed class IoTAlertHistoryEntry
{
    [BsonElement("at")]
    public DateTime At { get; set; }

    [BsonElement("by")]
    public string? By { get; set; }

    /// <summary>e.g. Raised, Acknowledged, Assigned, Escalated, Resolved, Closed, Auto-resolved.</summary>
    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;

    [BsonElement("note")]
    public string? Note { get; set; }
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
    /// <summary>New / unacknowledged.</summary>
    public const string Open = "Open";
    public const string Acknowledged = "Acknowledged";
    public const string Assigned = "Assigned";
    /// <summary>Investigating.</summary>
    public const string InProgress = "In Progress";
    public const string Resolved = "Resolved";
    /// <summary>Lifecycle completed.</summary>
    public const string Closed = "Closed";

    public static readonly IReadOnlyList<string> All = [Open, Acknowledged, Assigned, InProgress, Resolved, Closed];

    /// <summary>Statuses that still need someone: not resolved or closed.</summary>
    public static readonly IReadOnlyList<string> Unresolved = [Open, Acknowledged, Assigned, InProgress];
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
