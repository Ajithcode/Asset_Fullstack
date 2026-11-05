using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

// An asset or device placed on a floor plan; one per (floor plan, kind, item).
// Status is the item's own status (e.g. Active) and is stored in BaseEntity.Status.
[BsonIgnoreExtraElements]
public sealed class MapPlot : BaseEntity
{
    [BsonElement("floor_plan_id")]
    public string FloorPlanId { get; set; } = string.Empty;

    // asset or device.
    [BsonElement("kind")]
    public string Kind { get; set; } = string.Empty;

    // Id of the asset registration or IoT device being plotted.
    [BsonElement("item_id")]
    public string ItemId { get; set; } = string.Empty;

    [BsonElement("code")]
    public string? Code { get; set; }

    [BsonElement("name")]
    public string? Name { get; set; }

    [BsonElement("item_type")]
    public string? ItemType { get; set; }

    [BsonElement("asset_type")]
    public string? AssetType { get; set; }

    [BsonElement("model")]
    public string? Model { get; set; }

    [BsonElement("serial")]
    public string? Serial { get; set; }

    // Position, percent of the plan (0-100).
    [BsonElement("x")]
    public double X { get; set; }

    [BsonElement("y")]
    public double Y { get; set; }

    // Degrees.
    [BsonElement("orientation")]
    public double Orientation { get; set; }

    // Zone the item sits in, if any.
    [BsonElement("zone_id")]
    public string? ZoneId { get; set; }
}
