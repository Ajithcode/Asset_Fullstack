using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>A node in the asset hierarchy (e.g. a system, an asset within it, a component of that asset).</summary>
/// <remarks>
/// Items nest through ParentId (null for a top-level item). Asset type and model link to their masters in this
/// service; the manufacturer links to Master Management. Each keeps its name as it was when saved.
/// Status (Active/Inactive) is kept in BaseEntity.Status.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class AssetHierarchyItem : BaseEntity
{
    [BsonElement("item_kind")]
    public string ItemKind { get; set; } = string.Empty;

    [BsonElement("parent_id")]
    public string? ParentId { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("asset_tag")]
    public string? AssetTag { get; set; }

    [BsonElement("asset_type_id")]
    public string? AssetTypeId { get; set; }

    [BsonElement("asset_type_name")]
    public string? AssetTypeName { get; set; }

    [BsonElement("model_id")]
    public string? ModelId { get; set; }

    [BsonElement("model_name")]
    public string? ModelName { get; set; }

    [BsonElement("manufacturer_id")]
    public string? ManufacturerId { get; set; }

    [BsonElement("manufacturer_name")]
    public string? ManufacturerName { get; set; }
}
