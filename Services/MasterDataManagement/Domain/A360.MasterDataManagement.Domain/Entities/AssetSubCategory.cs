using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

/// <summary>
/// A sub-category of an asset category. Same fields as a category, kept in its own collection
/// and linked to its category by id (category_id), while its own id is the sub-category id.
/// </summary>
public class AssetSubCategory : BaseEntity
{
    [BsonElement("subcategory_name")]
    public string SubCategoryName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    [BsonElement("asset_type")]
    public string? AssetType { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("category_id")]
    public string CategoryId { get; set; } = null!;

    // Copied from the category so lists can show it without a lookup; kept in sync on category rename.
    [BsonElement("category_name")]
    public string CategoryName { get; set; } = null!;

    [BsonElement("assets")]
    public List<MasterDataReference> Assets { get; set; } = [];
}
