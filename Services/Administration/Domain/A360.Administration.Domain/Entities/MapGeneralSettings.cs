using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

/// <summary>
/// Maps &amp; Floor Plans &gt; Settings &gt; General Settings: global settings for maps, floor plans and spatial management.
/// One record per tenant and client.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class MapGeneralSettings : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("settings_id")]
    public string SettingsId { get; set; } = string.Empty;

    [BsonElement("map_management")]
    public MapManagementSettings MapManagement { get; set; } = new();

    [BsonElement("plotting_display")]
    public MapPlottingDisplaySettings PlottingDisplay { get; set; } = new();

    [BsonElement("zone_geofence")]
    public MapZoneGeofenceSettings ZoneGeofence { get; set; } = new();

    [BsonElement("versioning_publishing")]
    public MapVersioningSettings VersioningPublishing { get; set; } = new();

    [BsonElement("access_security")]
    public MapAccessSecuritySettings AccessSecurity { get; set; } = new();

    [BsonElement("notifications")]
    public MapNotificationSettings Notifications { get; set; } = new();
}

public sealed class MapManagementSettings
{
    // One of MapSettingsOptions.DefaultMapViews.
    [BsonElement("default_map_view")]
    public string DefaultMapView { get; set; } = "Fit to Floor Plan";

    // Percent, e.g. 100.
    [BsonElement("default_zoom_level")]
    public int DefaultZoomLevel { get; set; } = 100;

    // One of MapSettingsOptions.MeasurementUnits.
    [BsonElement("measurement_unit")]
    public string MeasurementUnit { get; set; } = "Metric (m, m²)";

    // Subset of MapSettingsOptions.FileTypes.
    [BsonElement("supported_file_types")]
    public List<string> SupportedFileTypes { get; set; } = ["PNG", "JPG", "PDF", "SVG"];

    [BsonElement("max_file_size_mb")]
    public int MaxFileSizeMb { get; set; } = 50;

    // One of MapSettingsOptions.CoordinateSystems.
    [BsonElement("default_coordinate_system")]
    public string DefaultCoordinateSystem { get; set; } = "Floor Plan (Local XY)";
}

public sealed class MapPlottingDisplaySettings
{
    // Subset of MapSettingsOptions.PlottedItems.
    [BsonElement("default_plotted_items")]
    public List<string> DefaultPlottedItems { get; set; } = ["Assets", "Devices", "Zones"];

    // One of MapSettingsOptions.AssetLabelDisplays.
    [BsonElement("asset_label_display")]
    public string AssetLabelDisplay { get; set; } = "Asset ID + Name";

    // One of MapLayerLabelPosition.All.
    [BsonElement("label_position")]
    public string LabelPosition { get; set; } = MapLayerLabelPosition.Default;

    [BsonElement("show_asset_status_icon")]
    public bool ShowAssetStatusIcon { get; set; } = true;

    [BsonElement("snap_to_grid")]
    public bool SnapToGrid { get; set; } = true;

    // Metres.
    [BsonElement("grid_size")]
    public double GridSize { get; set; } = 1.0;
}

public sealed class MapZoneGeofenceSettings
{
    // One of MapZoneType.All.
    [BsonElement("default_zone_type")]
    public string DefaultZoneType { get; set; } = "Operational";

    [BsonElement("default_geofence_dwell_time_minutes")]
    public int DefaultGeofenceDwellTimeMinutes { get; set; } = 30;

    // Hex colours offered when drawing zones; the first is the default.
    [BsonElement("zone_color_scheme")]
    public List<string> ZoneColorScheme { get; set; } = ["#7C3AED", "#3B82F6", "#22C55E", "#FACC15", "#F97316", "#F472B6"];

    [BsonElement("allow_zone_overlap")]
    public bool AllowZoneOverlap { get; set; }

    [BsonElement("geofence_event_logging")]
    public bool GeofenceEventLogging { get; set; } = true;
}

public sealed class MapVersioningSettings
{
    // One of MapSettingsOptions.VersionNumberings.
    [BsonElement("version_numbering")]
    public string VersionNumbering { get; set; } = "Semantic (v1.0, v1.1, v2.0)";

    [BsonElement("require_approval_to_publish")]
    public bool RequireApprovalToPublish { get; set; } = true;

    // Number of versions kept.
    [BsonElement("keep_version_history")]
    public int KeepVersionHistory { get; set; } = 10;

    [BsonElement("auto_archive_older_versions")]
    public bool AutoArchiveOlderVersions { get; set; } = true;

    [BsonElement("archive_after_days")]
    public int ArchiveAfterDays { get; set; } = 180;
}

public sealed class MapAccessSecuritySettings
{
    // One of MapSettingsOptions.MapAccessLevels.
    [BsonElement("default_map_access")]
    public string DefaultMapAccess { get; set; } = "View Only";

    [BsonElement("allow_download_of_map_files")]
    public bool AllowDownloadOfMapFiles { get; set; } = true;

    [BsonElement("watermark_published_maps")]
    public bool WatermarkPublishedMaps { get; set; }
}

public sealed class MapNotificationSettings
{
    [BsonElement("notify_on_map_publication")]
    public bool NotifyOnMapPublication { get; set; } = true;

    [BsonElement("notify_on_geofence_events")]
    public bool NotifyOnGeofenceEvents { get; set; } = true;

    [BsonElement("notify_on_version_change")]
    public bool NotifyOnVersionChange { get; set; }
}

public static class MapSettingsOptions
{
    public static readonly IReadOnlyList<string> DefaultMapViews =
        ["Fit to Floor Plan", "Fit to Width", "Fit to Height", "Actual Size", "Last Viewed"];

    public static readonly IReadOnlyList<string> MeasurementUnits = ["Metric (m, m²)", "Imperial (ft, ft²)"];

    public static readonly IReadOnlyList<string> FileTypes = ["PNG", "JPG", "PDF", "SVG", "DWG", "DXF"];

    public static readonly IReadOnlyList<string> CoordinateSystems =
        ["Floor Plan (Local XY)", "WGS 84 (Latitude / Longitude)", "UTM"];

    public static readonly IReadOnlyList<string> PlottedItems = ["Assets", "Devices", "Zones", "Annotations"];

    public static readonly IReadOnlyList<string> AssetLabelDisplays =
        ["Asset ID + Name", "Asset ID", "Asset Name", "None"];

    public static readonly IReadOnlyList<string> VersionNumberings =
        ["Semantic (v1.0, v1.1, v2.0)", "Sequential (v1, v2, v3)", "Date Based (YYYY.MM.DD)"];

    public static readonly IReadOnlyList<string> MapAccessLevels = ["No Access", "View Only", "Edit", "Full Access"];
}

public static class MapLayerLabelPosition
{
    public const string Default = "Bottom Center";

    public static readonly IReadOnlyList<string> All =
        ["Top Center", "Bottom Center", "Left", "Right", "Center", "Hidden"];
}
