using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

/// <summary>
/// Device &amp; Asset Plotting: an asset or device plotted on a floor plan (e.g. Chiller-01 (CH-001) in Production Zone, Building A – Floor 1).
/// </summary>
/// <remarks>Status (Active or Inactive) is kept in BaseEntity.Status.</remarks>
[BsonIgnoreExtraElements]
public sealed class DeviceAssetPlot : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("plot_id")]
    public string PlotId { get; set; } = string.Empty;

    // Floor plan the item is plotted on, e.g. Building A – Floor 1.
    [BsonElement("floor_plan_id")]
    public string? FloorPlanId { get; set; }

    [BsonElement("floor_plan_name")]
    public string? FloorPlanName { get; set; }

    // Asset or Device.
    [BsonElement("item_type")]
    public string ItemType { get; set; } = string.Empty;

    // Asset registration id or device id of the plotted item.
    [BsonElement("item_id")]
    public string? ItemId { get; set; }

    // e.g. CH-001.
    [BsonElement("item_code")]
    public string? ItemCode { get; set; }

    // e.g. Chiller-01.
    [BsonElement("item_name")]
    public string ItemName { get; set; } = string.Empty;

    // e.g. HVAC.
    [BsonElement("asset_category")]
    public string? AssetCategory { get; set; }

    // e.g. Chiller.
    [BsonElement("asset_type")]
    public string? AssetType { get; set; }

    [BsonElement("model")]
    public string? Model { get; set; }

    [BsonElement("serial_number")]
    public string? SerialNumber { get; set; }

    [BsonElement("location_id")]
    public string? LocationId { get; set; }

    // Location hierarchy, e.g. Head Office > Building A > Floor 1.
    [BsonElement("location_path")]
    public string LocationPath { get; set; } = string.Empty;

    [BsonElement("zone_id")]
    public string? ZoneId { get; set; }

    // e.g. PZ-01.
    [BsonElement("zone_code")]
    public string? ZoneCode { get; set; }

    // e.g. Production Zone.
    [BsonElement("zone_name")]
    public string? ZoneName { get; set; }

    // Map position in metres from the floor plan origin.
    [BsonElement("position_x")]
    public double PositionX { get; set; }

    [BsonElement("position_y")]
    public double PositionY { get; set; }

    // Rotation in degrees, 0 to 359.
    [BsonElement("orientation")]
    public double Orientation { get; set; }

    // Icon key shown on the map, e.g. cube, gateway, camera.
    [BsonElement("icon")]
    public string? Icon { get; set; }

    // Hex colour of the icon, e.g. #7C3AED.
    [BsonElement("color")]
    public string? Color { get; set; }
}

public static class DeviceAssetPlotItemType
{
    public const string Asset = "Asset";
    public const string Device = "Device";

    public static readonly IReadOnlyList<string> All = [Asset, Device];
}

public static class DeviceAssetPlotStatus
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";

    public static readonly IReadOnlyList<string> All = [Active, Inactive];
}
