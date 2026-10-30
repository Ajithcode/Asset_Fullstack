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

    /// <summary>Hex colour the zone is drawn in; one of <see cref="GeofenceMapColour.All"/>.</summary>
    [BsonElement("map_colour")]
    public string MapColour { get; set; } = GeofenceMapColour.Blue;

    /// <summary>Each one of <see cref="GeofenceAssetGroup.All"/> or a group in use; just "All assets" when every asset is monitored.</summary>
    [BsonElement("asset_groups")]
    public List<string> AssetGroups { get; set; } = [];

    /// <summary>One of <see cref="GeofenceSchedule.All"/>.</summary>
    [BsonElement("schedule")]
    public string Schedule { get; set; } = GeofenceSchedule.AlwaysActive;

    /// <summary>Raise a dwell alert when an asset stays in the zone this long.</summary>
    [BsonElement("dwell_alert_minutes")]
    public int? DwellAlertMinutes { get; set; }

    [BsonElement("monitor_entry")]
    public bool MonitorEntry { get; set; } = true;

    [BsonElement("monitor_exit")]
    public bool MonitorExit { get; set; } = true;

    /// <summary>One of <see cref="GeofenceShape.All"/>.</summary>
    [BsonElement("shape")]
    public string Shape { get; set; } = GeofenceShape.Polygon;

    /// <summary>Number of corners for a polygon; matches <see cref="Coordinates"/> once the outline is drawn.</summary>
    [BsonElement("polygon_points")]
    public int? PolygonPoints { get; set; }

    /// <summary>
    /// Outline drawn on the map: the corners of a polygon or rectangle, or the single centre of a circle.
    /// Empty until the zone is drawn.
    /// </summary>
    [BsonElement("coordinates")]
    public List<GeofencePoint> Coordinates { get; set; } = [];

    /// <summary>Only used by circles.</summary>
    [BsonElement("radius_meters")]
    public double? RadiusMeters { get; set; }
}

public sealed class GeofencePoint
{
    [BsonElement("latitude")]
    public double Latitude { get; set; }

    [BsonElement("longitude")]
    public double Longitude { get; set; }
}

public static class GeofenceType
{
    public const string Allowed = "Allowed";
    public const string Warning = "Warning";
    public const string Restricted = "Restricted";

    public static readonly IReadOnlyList<string> All = [Allowed, Warning, Restricted];
}

public static class GeofenceMapColour
{
    public const string Blue = "#2563EB";
    public const string Amber = "#F5B40B";
    public const string Red = "#EF4444";
    public const string Teal = "#0D9488";
    public const string Purple = "#7C3AED";
    public const string Orange = "#F97316";
    public const string Pink = "#EC4899";
    public const string Cyan = "#0891B2";

    public static readonly IReadOnlyList<string> All = [Blue, Amber, Red, Teal, Purple, Orange, Pink, Cyan];
}

public static class GeofenceAssetGroup
{
    public const string Forklifts = "Forklifts";
    public const string MobileTools = "Mobile Tools";
    public const string Vehicles = "Vehicles";
    public const string AllAssets = "All assets";

    public static readonly IReadOnlyList<string> All = [Forklifts, MobileTools, Vehicles, AllAssets];
}

public static class GeofenceSchedule
{
    public const string AlwaysActive = "Always active";
    public const string WorkingHours = "During working hours";
    public const string OutsideWorkingHours = "Outside working hours";

    public static readonly IReadOnlyList<string> All = [AlwaysActive, WorkingHours, OutsideWorkingHours];
}

public static class GeofenceShape
{
    public const string Polygon = "Polygon";
    public const string Rectangle = "Rectangle";
    public const string Circle = "Circle";

    public static readonly IReadOnlyList<string> All = [Polygon, Rectangle, Circle];

    public const int DefaultPolygonPoints = 4;
    public const int MinPolygonPoints = 3;
    public const int MaxPolygonPoints = 50;
}

public static class GeofenceStatus
{
    public const string Active = "Active";
    public const string Disabled = "Disabled";

    public static readonly IReadOnlyList<string> All = [Active, Disabled];
}
