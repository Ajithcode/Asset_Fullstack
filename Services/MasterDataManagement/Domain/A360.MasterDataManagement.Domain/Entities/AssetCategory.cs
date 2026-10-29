using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class AssetCategory : BaseEntity
{
    [BsonElement("category_name")]
    public string CategoryName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    [BsonElement("asset_type")]
    public string? AssetType { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // Points at another asset category, so categories form a tree.
    [BsonElement("parent_category")]
    public MasterDataReference? ParentCategory { get; set; }

    // Assets live in the Asset service, so these are kept as typed and not checked here.
    [BsonElement("assets")]
    public List<MasterDataReference> Assets { get; set; } = [];
}
