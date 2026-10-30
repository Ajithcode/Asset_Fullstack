using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class ItemCategory : BaseEntity
{
    [BsonElement("category_name")]
    public string CategoryName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Points at another item category, so categories form a tree.
    [BsonElement("parent_category")]
    public MasterDataReference? ParentCategory { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // Unit of measure new items in this category start with.
    [BsonElement("default_uom")]
    public MasterDataReference? DefaultUom { get; set; }

    // e.g. "Weighted Average", "FIFO" or "Standard Cost".
    [BsonElement("valuation_method")]
    public string? ValuationMethod { get; set; }

    [BsonElement("inspection_on_receipt")]
    public bool InspectionOnReceipt { get; set; }

    [BsonElement("allow_negative_stock")]
    public bool AllowNegativeStock { get; set; }
}
