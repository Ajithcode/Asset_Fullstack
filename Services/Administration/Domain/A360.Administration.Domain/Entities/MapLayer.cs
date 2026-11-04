using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

/// <summary>
/// Layers: a toggleable layer of a floor plan (e.g. Assets, of type Asset (Plotted / Tracked), filtered to HVAC).
/// </summary>
/// <remarks>Status (Active or Inactive) is kept in BaseEntity.Status; visibility on the map is <see cref="IsVisible"/>.</remarks>
[BsonIgnoreExtraElements]
public sealed class MapLayer : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("layer_id")]
    public string LayerId { get; set; } = string.Empty;

    // Floor plan the layer belongs to, e.g. Building A – Floor 1.
    [BsonElement("floor_plan_id")]
    public string? FloorPlanId { get; set; }

    [BsonElement("floor_plan_name")]
    public string? FloorPlanName { get; set; }

    // e.g. Head Office > Building A > Floor 1.
    [BsonElement("location_path")]
    public string? LocationPath { get; set; }

    // e.g. Assets.
    [BsonElement("layer_name")]
    public string LayerName { get; set; } = string.Empty;

    // One of MapLayerType.All.
    [BsonElement("layer_type")]
    public string LayerType { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; }

    // The Visible toggle.
    [BsonElement("is_visible")]
    public bool IsVisible { get; set; } = true;

    [BsonElement("display_options")]
    public MapLayerDisplayOptions DisplayOptions { get; set; } = new();

    [BsonElement("filters")]
    public MapLayerFilters Filters { get; set; } = new();
}

public sealed class MapLayerDisplayOptions
{
    [BsonElement("show_asset_label")]
    public bool ShowAssetLabel { get; set; }

    [BsonElement("show_asset_id")]
    public bool ShowAssetId { get; set; }

    [BsonElement("show_asset_status_icon")]
    public bool ShowAssetStatusIcon { get; set; }

    [BsonElement("use_category_color")]
    public bool UseCategoryColor { get; set; }

    // One of MapLayerLabelPosition.All.
    [BsonElement("label_position")]
    public string LabelPosition { get; set; } = MapLayerLabelPosition.Default;
}

public sealed class MapLayerFilters
{
    // e.g. HVAC; empty means every category.
    [BsonElement("asset_categories")]
    public List<string> AssetCategories { get; set; } = [];

    // One of MapLayerAssetStatus.All; null means All Statuses.
    [BsonElement("asset_status")]
    public string? AssetStatus { get; set; }
}

public static class MapLayerType
{
    public static readonly IReadOnlyList<string> All =
    [
        "Zone", "Geofence", "Asset (Plotted / Tracked)", "Device", "Annotation", "Room & Space",
        "Floor Plan (Base)", "Heatmap (Utilization)"
    ];
}

public static class MapLayerLabelPosition
{
    public const string Default = "Bottom Center";

    public static readonly IReadOnlyList<string> All =
        ["Top Center", "Bottom Center", "Left", "Right", "Center", "Hidden"];
}

public static class MapLayerAssetStatus
{
    // Shown in the dropdown; stored as null.
    public const string AllStatuses = "All Statuses";

    // Same values as the asset registration Status dropdown.
    public static readonly IReadOnlyList<string> All =
        ["Active", "Inactive", "In Storage", "Under Maintenance", "In Repair", "Out of Service", "Disposed"];
}

public static class MapLayerStatus
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";

    public static readonly IReadOnlyList<string> All = [Active, Inactive];
}
