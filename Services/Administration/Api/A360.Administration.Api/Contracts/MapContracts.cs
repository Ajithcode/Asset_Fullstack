using System.Text.Json;
using System.Text.Json.Serialization;
using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Contracts;

/// <summary>Allowed values of the Maps &amp; Floor Plans enumerations, in display order.</summary>
public static class MapOptions
{
    public static readonly string[] LocationTypes = ["site", "building", "floor"];
    public static readonly string[] FloorPlanTypes = ["Floor Plan", "Site Map"];
    public static readonly string[] FileFormats = ["PNG", "JPG", "PDF", "SVG", "DWG", "DXF"];
    public static readonly string[] Statuses = ["Draft", "Published", "Archived"];
    public static readonly string[] ZoneTypes = ["Production", "Office", "Storage", "Restricted", "Maintenance", "Operational"];
    public static readonly string[] ZoneShapes = ["Polygon", "Rectangle", "Circle", "Custom"];
    public static readonly string[] AccessRules = ["Authorized Personnel Only", "All Staff", "Contractors with Escort", "Restricted - Badge Required"];
    public static readonly string[] PlotKinds = ["asset", "device"];
    public static readonly string[] VersionBumps = ["minor", "major"];
    public static readonly string[] ZoneColors = ["#7C3AED", "#2563EB", "#10B981", "#F59E0B", "#F97316", "#EC4899"];

    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Archived = "Archived";

    /// <summary>The eight layers, in the order they are listed; Locked is the default lock state.</summary>
    public static readonly LayerDefinition[] Layers =
    [
        new("zones", "Zones", "Zone (Area)", true, "Operational zones drawn on the floor plan."),
        new("geofences", "Geofences", "Geofence (Alerting)", true, "Geofence boundaries that raise entry / exit alerts."),
        new("assets", "Assets", "Asset (Plotted / Tracked)", true, "Displays assets and major equipment on the floor plan."),
        new("devices", "Devices", "Device (IoT)", true, "Gateways, access points, cameras, sensors and meters."),
        new("annotations", "Annotations", "Annotation (Text)", false, "Free text notes and dimension markers."),
        new("rooms", "Room & Space", "Room (Space)", true, "Rooms and spaces from the location hierarchy."),
        new("base", "Floor Plan (Base)", "Base Map", true, "The uploaded floor plan image.", Locked: true),
        new("heatmap", "Heatmap (Utilization)", "Heatmap", false, "Space utilization heatmap from occupancy sensors.")
    ];
}

public sealed record LayerDefinition(string Key, string Name, string Type, bool Enabled, string Description, bool Locked = false);

// GET /api/administration/maps/summary
public sealed record MapSummaryResponse(int FloorPlans, int Zones, int DevicesPlotted, int AssetsPlotted);

// POST /api/administration/maps/floor-plans
public sealed record CreateFloorPlanRequest(
    string? LocationId,
    string? LocationType,
    string? LocationPath,
    string? Name,
    string? Description,
    string? Type,
    string? FileUrl,
    string? FileName,
    string? Format,
    long? SizeBytes,
    double? WidthMeters,
    double? HeightMeters,
    string? Scale,
    string? Status,
    string? VersionNote,
    string? CreatedBy,
    string? ClientId,
    string? TenantId);

// PUT /api/administration/maps/floor-plans/{floorPlanId}
public sealed record UpdateFloorPlanRequest(
    string? Name,
    string? Description,
    string? Type,
    double? WidthMeters,
    double? HeightMeters,
    string? Scale,
    string? LocationId,
    string? LocationType,
    string? LocationPath,
    string? UpdatedBy);

// Body of publish / archive / restore actions.
public sealed record MapActionRequest(string? UpdatedBy);

public sealed record FloorPlanResponse(
    string FloorPlanId,
    string LocationId,
    string LocationType,
    string? LocationPath,
    string Name,
    string? Description,
    string Type,
    string Format,
    string? FileUrl,
    string FileName,
    long SizeBytes,
    double? WidthMeters,
    double? HeightMeters,
    string? Scale,
    string CurrentVersion,
    string? Status,
    DateTime? PublishedAt,
    string? PublishedBy,
    int ZoneCount,
    int PlotCount,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static FloorPlanResponse FromEntity(FloorPlan plan, int zoneCount, int plotCount)
    {
        return new FloorPlanResponse(
            plan.FloorPlanId,
            plan.LocationId,
            plan.LocationType,
            plan.LocationPath,
            plan.Name,
            plan.Description,
            plan.PlanType,
            plan.Format,
            plan.FileUrl,
            plan.FileName,
            plan.SizeBytes,
            plan.WidthMeters,
            plan.HeightMeters,
            plan.Scale,
            plan.CurrentVersion,
            plan.Status,
            plan.PublishedAt,
            plan.PublishedBy,
            zoneCount,
            plotCount,
            plan.CreatedBy,
            plan.CreatedAt,
            plan.UpdatedBy,
            plan.UpdatedAt,
            plan.ClientId,
            plan.TenantId);
    }
}

// POST /api/administration/maps/floor-plans/{floorPlanId}/versions
public sealed record CreateVersionRequest(
    string? FileUrl,
    string? FileName,
    string? Format,
    long? SizeBytes,
    string? Bump,
    string? Description,
    List<string>? Changes,
    string? CreatedBy);

public sealed record VersionResponse(
    string Version,
    bool Current,
    string Status,
    string? FileUrl,
    string FileName,
    string Format,
    long SizeBytes,
    string? Description,
    IReadOnlyList<string> Changes,
    string? UploadedBy,
    DateTime? UploadedAt)
{
    public static VersionResponse FromEntity(FloorPlanVersion version, string currentVersion)
    {
        return new VersionResponse(
            version.Version,
            string.Equals(version.Version, currentVersion, StringComparison.OrdinalIgnoreCase),
            version.Status,
            version.FileUrl,
            version.FileName,
            version.Format,
            version.SizeBytes,
            version.Description,
            version.Changes,
            version.UploadedBy,
            version.UploadedAt);
    }
}

// GET /api/administration/maps/zones/options
public sealed record ZoneOptionsResponse(
    IReadOnlyList<string> ZoneTypes,
    IReadOnlyList<string> Shapes,
    IReadOnlyList<string> AccessRules,
    IReadOnlyList<string> Colors);

public sealed record MapPointDto(double X, double Y);

// POST /api/administration/maps/floor-plans/{floorPlanId}/zones and PUT /api/administration/maps/zones/{zoneId}
public sealed record SaveZoneRequest(
    string? Code,
    string? Name,
    string? Type,
    string? Color,
    string? Shape,
    double? X,
    double? Y,
    double? W,
    double? H,
    List<MapPointDto>? Points,
    double? Radius,
    double? AreaM2,
    string? Description,
    string? AccessRule,
    bool? Active,
    bool? Geofence,
    bool? EntryAlert,
    bool? ExitAlert,
    bool? DwellAlert,
    int? DwellMinutes,
    string? CreatedBy,
    string? UpdatedBy,
    string? ClientId,
    string? TenantId);

public sealed record ZoneResponse(
    string ZoneId,
    string FloorPlanId,
    string Code,
    string Name,
    string Type,
    string? Color,
    string Shape,
    double X,
    double Y,
    double W,
    double H,
    IReadOnlyList<MapPointDto> Points,
    double? Radius,
    double? AreaM2,
    string? Description,
    string? AccessRule,
    bool Active,
    bool Geofence,
    bool EntryAlert,
    bool ExitAlert,
    bool DwellAlert,
    int DwellMinutes,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static ZoneResponse FromEntity(MapZone zone)
    {
        return new ZoneResponse(
            zone.ZoneId,
            zone.FloorPlanId,
            zone.Code,
            zone.Name,
            zone.ZoneType,
            zone.Color,
            zone.Shape,
            zone.X,
            zone.Y,
            zone.W,
            zone.H,
            zone.Points.Select(point => new MapPointDto(point.X, point.Y)).ToList(),
            zone.Radius,
            zone.AreaM2,
            zone.Description,
            zone.AccessRule,
            zone.Active,
            zone.Geofence,
            zone.EntryAlert,
            zone.ExitAlert,
            zone.DwellAlert,
            zone.DwellMinutes,
            zone.CreatedBy,
            zone.CreatedAt,
            zone.UpdatedBy,
            zone.UpdatedAt,
            zone.ClientId,
            zone.TenantId);
    }
}

// PUT /api/administration/maps/floor-plans/{floorPlanId}/plots/{kind}/{itemId}
public sealed record SavePlotRequest(
    string? Code,
    string? Name,
    string? Type,
    string? AssetType,
    string? Model,
    string? Serial,
    string? Status,
    double? X,
    double? Y,
    double? Orientation,
    string? ZoneId,
    string? UpdatedBy,
    string? ClientId,
    string? TenantId);

public sealed record PlotResponse(
    string? Code,
    string? Name,
    string? Type,
    string? AssetType,
    string? Model,
    string? Serial,
    string? Status,
    double X,
    double Y,
    double Orientation,
    string? ZoneId,
    string? UpdatedBy,
    string? ClientId,
    string? TenantId,
    string FloorPlanId,
    string Kind,
    string ItemId,
    DateTime? UpdatedAt)
{
    public static PlotResponse FromEntity(MapPlot plot)
    {
        return new PlotResponse(
            plot.Code,
            plot.Name,
            plot.ItemType,
            plot.AssetType,
            plot.Model,
            plot.Serial,
            plot.Status,
            plot.X,
            plot.Y,
            plot.Orientation,
            plot.ZoneId,
            plot.UpdatedBy,
            plot.ClientId,
            plot.TenantId,
            plot.FloorPlanId,
            plot.Kind,
            plot.ItemId,
            plot.UpdatedAt);
    }
}

// PUT /api/administration/maps/floor-plans/{floorPlanId}/layers/{layerKey}; fields left out keep their value.
public sealed record SaveLayerRequest(
    bool? Enabled,
    bool? Visible,
    bool? Locked,
    bool? ShowLabel,
    bool? ShowId,
    bool? ShowStatusIcon,
    bool? UseCategoryColor,
    string? LabelPosition,
    List<string>? Categories,
    string? StatusFilter,
    double? Opacity,
    string? Color,
    string? UpdatedBy);

public sealed record LayerResponse(
    string Key,
    string Name,
    string Type,
    string Description,
    bool Enabled,
    bool Visible,
    bool Locked,
    bool ShowLabel,
    bool ShowId,
    bool ShowStatusIcon,
    bool UseCategoryColor,
    string? LabelPosition,
    IReadOnlyList<string> Categories,
    string? StatusFilter,
    int Opacity,
    string? Color)
{
    public static LayerResponse FromEntity(LayerDefinition definition, MapLayer layer)
    {
        return new LayerResponse(
            definition.Key,
            definition.Name,
            definition.Type,
            definition.Description,
            layer.Enabled,
            layer.Visible,
            layer.Locked,
            layer.ShowLabel,
            layer.ShowId,
            layer.ShowStatusIcon,
            layer.UseCategoryColor,
            layer.LabelPosition,
            layer.Categories,
            layer.StatusFilter,
            layer.Opacity,
            layer.Color);
    }
}

// POST /api/administration/maps/layer-sets
public sealed record CreateLayerSetRequest(
    string? Name,
    Dictionary<string, bool>? Layers,
    string? CreatedBy,
    string? ClientId,
    string? TenantId);

public sealed record LayerSetResponse(
    string LayerSetId,
    string Name,
    IReadOnlyDictionary<string, bool> Layers,
    string? CreatedBy,
    DateTime? CreatedAt)
{
    public static LayerSetResponse FromEntity(MapLayerSet set)
    {
        return new LayerSetResponse(set.LayerSetId, set.Name, set.Layers, set.CreatedBy, set.CreatedAt);
    }
}

/// <summary>
/// PUT /api/administration/maps/settings. Flat Settings-tab values; fields left out keep their saved value.
/// Keys the service doesn't know are kept in Extra (sent either inside "extra" or at the top level).
/// </summary>
public sealed class SaveMapSettingsRequest
{
    public string? DefaultView { get; set; }
    public double? DefaultZoom { get; set; }
    public string? Unit { get; set; }
    public Dictionary<string, bool>? FileTypes { get; set; }
    public double? MaxFileMb { get; set; }
    public string? CoordinateSystem { get; set; }
    public Dictionary<string, bool>? Plotted { get; set; }
    public string? AssetLabel { get; set; }
    public string? LabelPosition { get; set; }
    public bool? StatusIcon { get; set; }
    public bool? Snap { get; set; }
    public double? GridSize { get; set; }
    public string? ZoneType { get; set; }
    public double? DwellMinutes { get; set; }
    public string? ZoneColor { get; set; }
    public bool? ZoneOverlap { get; set; }
    public bool? GeofenceLogging { get; set; }
    public string? Versioning { get; set; }
    public bool? Approval { get; set; }
    public double? KeepVersions { get; set; }
    public bool? AutoArchive { get; set; }
    public double? ArchiveAfter { get; set; }
    public string? MapAccess { get; set; }
    public bool? AllowDownload { get; set; }
    public bool? Watermark { get; set; }
    public bool? NotifyPublish { get; set; }
    public bool? NotifyGeofence { get; set; }
    public bool? NotifyVersion { get; set; }
    public double? CalibrationPoints { get; set; }
    public double? CalibrationTolerance { get; set; }
    public double? OriginX { get; set; }
    public double? OriginY { get; set; }
    public double? Rotation { get; set; }
    public Dictionary<string, bool>? Integrations { get; set; }
    public bool? TileCache { get; set; }
    public double? CacheHours { get; set; }
    public bool? AuditEdits { get; set; }
    public Dictionary<string, JsonElement>? Extra { get; set; }

    public string? UpdatedBy { get; set; }
    public string? ClientId { get; set; }
    public string? TenantId { get; set; }

    // Top-level keys that match none of the properties above.
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Unknown { get; set; }
}

public sealed record MapSettingsResponse(
    string DefaultView,
    double DefaultZoom,
    string Unit,
    IReadOnlyDictionary<string, bool> FileTypes,
    double MaxFileMb,
    string CoordinateSystem,
    IReadOnlyDictionary<string, bool> Plotted,
    string AssetLabel,
    string LabelPosition,
    bool StatusIcon,
    bool Snap,
    double GridSize,
    string ZoneType,
    double DwellMinutes,
    string ZoneColor,
    bool ZoneOverlap,
    bool GeofenceLogging,
    string Versioning,
    bool Approval,
    double KeepVersions,
    bool AutoArchive,
    double ArchiveAfter,
    string MapAccess,
    bool AllowDownload,
    bool Watermark,
    bool NotifyPublish,
    bool NotifyGeofence,
    bool NotifyVersion,
    double CalibrationPoints,
    double CalibrationTolerance,
    double OriginX,
    double OriginY,
    double Rotation,
    IReadOnlyDictionary<string, bool> Integrations,
    bool TileCache,
    double CacheHours,
    bool AuditEdits,
    IReadOnlyDictionary<string, object?> Extra)
{
    public static MapSettingsResponse FromEntity(MapSettings settings)
    {
        return new MapSettingsResponse(
            settings.DefaultView,
            settings.DefaultZoom,
            settings.Unit,
            settings.FileTypes,
            settings.MaxFileMb,
            settings.CoordinateSystem,
            settings.Plotted,
            settings.AssetLabel,
            settings.LabelPosition,
            settings.StatusIcon,
            settings.Snap,
            settings.GridSize,
            settings.ZoneType,
            settings.DwellMinutes,
            settings.ZoneColor,
            settings.ZoneOverlap,
            settings.GeofenceLogging,
            settings.Versioning,
            settings.Approval,
            settings.KeepVersions,
            settings.AutoArchive,
            settings.ArchiveAfter,
            settings.MapAccess,
            settings.AllowDownload,
            settings.Watermark,
            settings.NotifyPublish,
            settings.NotifyGeofence,
            settings.NotifyVersion,
            settings.CalibrationPoints,
            settings.CalibrationTolerance,
            settings.OriginX,
            settings.OriginY,
            settings.Rotation,
            settings.Integrations,
            settings.TileCache,
            settings.CacheHours,
            settings.AuditEdits,
            MapMappings.ToPlainDictionary(settings.Extra));
    }
}
