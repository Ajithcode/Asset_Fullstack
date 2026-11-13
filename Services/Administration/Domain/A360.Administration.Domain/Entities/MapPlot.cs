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

    // Name / code of that drawn zone (maps_zones), copied on save.
    [BsonElement("zone_name")]
    public string? ZoneName { get; set; }

    [BsonElement("zone_code")]
    public string? ZoneCode { get; set; }

    // Site > building > floor > Master Data zone where the item is plotted (set by the server on save).
    [BsonElement("location")]
    public MapLocation? Location { get; set; }

    // Devices: the device's unique id (IoTDevice.device_id, e.g. AC233FC28E11) and its record's Mongo id.
    [BsonElement("device_id")]
    public string? DeviceId { get; set; }

    [BsonElement("device_record_id")]
    public string? DeviceRecordId { get; set; }

    // Assets: the plotted asset; devices: the asset the device is linked to in IoT Device Management.
    [BsonElement("asset_registration_id")]
    public string? AssetRegistrationId { get; set; }
}
