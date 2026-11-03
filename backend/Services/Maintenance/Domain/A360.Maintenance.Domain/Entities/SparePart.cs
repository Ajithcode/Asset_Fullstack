
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Maintenance.Domain.Entities;

public class SparePart : BaseEntity
{
    [BsonElement("part_id")]
    public string PartId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string? AssetId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("part_name")]
    public string PartName { get; set; } = null!;

    [BsonElement("category")]
    public string? Category { get; set; }

    [BsonElement("quantity_in_stock")]
    public int QuantityInStock { get; set; }

    [BsonElement("minimum_stock_level")]
    public int MinimumStockLevel { get; set; }

    [BsonElement("unit_cost")]
    public double UnitCost { get; set; }

    [BsonElement("supplier")]
    public string? Supplier { get; set; }
}
