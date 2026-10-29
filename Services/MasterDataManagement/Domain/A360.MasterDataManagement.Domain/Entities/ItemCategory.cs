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
}
