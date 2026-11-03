using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Tracking.Domain.Entities;

/// <summary>Geofence & Alerts → Geofence List: a monitored zone (e.g. "Restricted Zone" at Dubai Plant).</summary>
/// <remarks>Names are unique within a site. Status is kept in BaseEntity.Status and is one of <see cref="GeofenceStatus"/>.</remarks>
[BsonIgnoreExtraElements]
public sealed class Geofence : BaseEntity
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>One of <see cref="GeofenceType.All"/>.</summary>
    [BsonElement("geofence_type")]
    public string GeofenceType { get; set; } = string.Empty;

    [BsonElement("site_id")]
    public string? SiteId { get; set; }

    [BsonElement("site_name")]
    public string SiteName { get; set; } = string.Empty;

    [BsonElement("monitored_asset_count")]
    public int MonitoredAssetCount { get; set; }

    [BsonElement("rule_count")]
    public int RuleCount { get; set; }

    /// <summary>When an asset last entered, exited or dwelled in the zone.</summary>
    [BsonElement("last_event_at")]
    public DateTime? LastEventAt { get; set; }
}

public static class GeofenceType
{
    public const string Allowed = "Allowed";
    public const string Warning = "Warning";
    public const string Restricted = "Restricted";

    public static readonly IReadOnlyList<string> All = [Allowed, Warning, Restricted];
}

public static class GeofenceStatus
{
    public const string Active = "Active";
    public const string Disabled = "Disabled";

    public static readonly IReadOnlyList<string> All = [Active, Disabled];
}
