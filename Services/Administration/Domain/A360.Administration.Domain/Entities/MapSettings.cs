using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

// One document per tenant/client holding the Maps & Floor Plans settings.
// Property initializers are the defaults, so missing fields (and a missing document) read as defaults.
[BsonIgnoreExtraElements]
public sealed class MapSettings : BaseEntity
{
    // Business identifier; kept separate from the Mongo _id.
    [BsonElement("settings_id")]
    public string SettingsId { get; set; } = string.Empty;

    // General
    [BsonElement("default_view")]
    public string DefaultView { get; set; } = "Fit to Floor Plan";

    [BsonElement("default_zoom")]
    public double DefaultZoom { get; set; } = 100;

    [BsonElement("unit")]
    public string Unit { get; set; } = "Metric (m, m²)";

    // File format -> allowed.
    [BsonElement("file_types")]
    public Dictionary<string, bool> FileTypes { get; set; } = new()
    {
        ["PNG"] = true, ["JPG"] = true, ["PDF"] = true, ["SVG"] = true, ["DWG"] = false, ["DXF"] = false
    };

    [BsonElement("max_file_mb")]
    public double MaxFileMb { get; set; } = 50;

    [BsonElement("coordinate_system")]
    public string CoordinateSystem { get; set; } = "Floor Plan (Local XY)";

    // Map & plotting: what can be plotted -> on/off.
    [BsonElement("plotted")]
    public Dictionary<string, bool> Plotted { get; set; } = new()
    {
        ["Assets"] = true, ["Devices"] = true, ["Zones"] = true, ["Annotations"] = false
    };

    [BsonElement("asset_label")]
    public string AssetLabel { get; set; } = "Asset ID + Name";

    [BsonElement("label_position")]
    public string LabelPosition { get; set; } = "Bottom Center";

    [BsonElement("status_icon")]
    public bool StatusIcon { get; set; } = true;

    [BsonElement("snap")]
    public bool Snap { get; set; } = true;

    [BsonElement("grid_size")]
    public double GridSize { get; set; } = 1;

    // Zones & geofences
    [BsonElement("zone_type")]
    public string ZoneType { get; set; } = "Operational";

    [BsonElement("dwell_minutes")]
    public double DwellMinutes { get; set; } = 30;

    [BsonElement("zone_color")]
    public string ZoneColor { get; set; } = "#7c3aed";

    [BsonElement("zone_overlap")]
    public bool ZoneOverlap { get; set; }

    [BsonElement("geofence_logging")]
    public bool GeofenceLogging { get; set; } = true;

    // Versioning
    [BsonElement("versioning")]
    public string Versioning { get; set; } = "Semantic (v1.0, v1.1, v2.0)";

    [BsonElement("approval")]
    public bool Approval { get; set; } = true;

    [BsonElement("keep_versions")]
    public double KeepVersions { get; set; } = 10;

    [BsonElement("auto_archive")]
    public bool AutoArchive { get; set; } = true;

    // Days.
    [BsonElement("archive_after")]
    public double ArchiveAfter { get; set; } = 180;

    // Access & permissions
    [BsonElement("map_access")]
    public string MapAccess { get; set; } = "View Only";

    [BsonElement("allow_download")]
    public bool AllowDownload { get; set; } = true;

    [BsonElement("watermark")]
    public bool Watermark { get; set; }

    // Notifications
    [BsonElement("notify_publish")]
    public bool NotifyPublish { get; set; } = true;

    [BsonElement("notify_geofence")]
    public bool NotifyGeofence { get; set; } = true;

    [BsonElement("notify_version")]
    public bool NotifyVersion { get; set; }

    // Coordinate calibration
    [BsonElement("calibration_points")]
    public double CalibrationPoints { get; set; } = 3;

    [BsonElement("calibration_tolerance")]
    public double CalibrationTolerance { get; set; } = 0.5;

    [BsonElement("origin_x")]
    public double OriginX { get; set; }

    [BsonElement("origin_y")]
    public double OriginY { get; set; }

    [BsonElement("rotation")]
    public double Rotation { get; set; }

    // Integration name -> on/off.
    [BsonElement("integrations")]
    public Dictionary<string, bool> Integrations { get; set; } = new()
    {
        ["BMS / BACnet"] = false, ["RTLS (BLE)"] = true, ["CAFM / IWMS"] = false, ["Autodesk (DWG import)"] = true
    };

    // Advanced
    [BsonElement("tile_cache")]
    public bool TileCache { get; set; } = true;

    [BsonElement("cache_hours")]
    public double CacheHours { get; set; } = 24;

    [BsonElement("audit_edits")]
    public bool AuditEdits { get; set; } = true;

    // Any other Settings-tab keys, stored as sent.
    [BsonElement("extra")]
    public BsonDocument Extra { get; set; } = new();
}
