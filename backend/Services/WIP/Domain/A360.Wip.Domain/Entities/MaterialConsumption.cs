
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Wip.Domain.Entities;

public class MaterialConsumption : BaseEntity
{
    [BsonElement("material_id")]
    public string MaterialId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string? AssetId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("job_id")]
    public string? JobId { get; set; }

    [BsonElement("task_id")]
    public string? TaskId { get; set; }

    [BsonElement("item_name")]
    public string? ItemName { get; set; }

    [BsonElement("item_code")]
    public string? ItemCode { get; set; }

    [BsonElement("quantity_planned")]
    public double? QuantityPlanned { get; set; }

    [BsonElement("quantity_used")]
    public double? QuantityUsed { get; set; }

    [BsonElement("unit")]
    public string? Unit { get; set; }

    [BsonElement("cost")]
    public double? Cost { get; set; }

    [BsonElement("vendor_id")]
    public string? VendorId { get; set; }
}
