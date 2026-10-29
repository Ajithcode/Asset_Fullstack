using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

// An inventory item: a spare part or consumable used in maintenance.
public class Item : BaseEntity
{
    [BsonElement("item_name")]
    public string ItemName { get; set; } = null!;

    [BsonElement("item_code")]
    public string ItemCode { get; set; } = null!;

    [BsonElement("part_number")]
    public string? PartNumber { get; set; }

    // Points at an item category in this service.
    [BsonElement("category")]
    public MasterDataReference? Category { get; set; }

    // Points at a unit of measure in this service.
    [BsonElement("uom")]
    public MasterDataReference? Uom { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // URL of a photo uploaded through /api/media/upload.
    [BsonElement("image_url")]
    public string? ImageUrl { get; set; }

    [BsonElement("current_stock")]
    public double? CurrentStock { get; set; }

    // Stock level at which the item should be reordered.
    [BsonElement("reorder_level")]
    public double? ReorderLevel { get; set; }
}
