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

    [BsonElement("min_stock")]
    public double? MinStock { get; set; }

    // How much to order when the item is reordered.
    [BsonElement("reorder_qty")]
    public double? ReorderQty { get; set; }

    [BsonElement("max_stock")]
    public double? MaxStock { get; set; }

    // Where the stock is kept: a warehouse, store and bin in this service.
    [BsonElement("warehouse")]
    public MasterDataReference? Warehouse { get; set; }

    [BsonElement("store")]
    public MasterDataReference? Store { get; set; }

    [BsonElement("bin")]
    public MasterDataReference? Bin { get; set; }

    [BsonElement("unit_cost")]
    public double? UnitCost { get; set; }

    [BsonElement("currency")]
    public string? Currency { get; set; }

    [BsonElement("valuation_method")]
    public string? ValuationMethod { get; set; }

    [BsonElement("manufacturer")]
    public string? Manufacturer { get; set; }

    [BsonElement("specification")]
    public string? Specification { get; set; }

    [BsonElement("shelf_life_days")]
    public int? ShelfLifeDays { get; set; }

    [BsonElement("hazardous")]
    public bool Hazardous { get; set; }

    [BsonElement("serialized")]
    public bool Serialized { get; set; }

    [BsonElement("batch_tracked")]
    public bool BatchTracked { get; set; }

    [BsonElement("purchasable")]
    public bool Purchasable { get; set; } = true;

    [BsonElement("issuable")]
    public bool Issuable { get; set; } = true;

    [BsonElement("critical_spare")]
    public bool CriticalSpare { get; set; }
}
