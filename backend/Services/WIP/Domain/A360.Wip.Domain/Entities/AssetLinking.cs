
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Wip.Domain.Entities;

public class AssetLinking : BaseEntity
{
    [BsonElement("asset_id")]
    public string AssetId { get; set; } = null!;

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("asset_type")]
    public string? AssetType { get; set; }

    [BsonElement("serial_number")]
    public string? SerialNumber { get; set; }

    [BsonElement("rfid_tag")]
    public string? RfidTag { get; set; }

    [BsonElement("iot_device_id")]
    public string? IotDeviceId { get; set; }

    [BsonElement("current_status")]
    public string? CurrentStatus { get; set; }

    [BsonElement("utilization_status")]
    public string? UtilizationStatus { get; set; }

    [BsonElement("last_maintenance_date")]
    public DateTime? LastMaintenanceDate { get; set; }

    [BsonElement("condition")]
    public string? Condition { get; set; }
}
