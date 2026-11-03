using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>How fields are arranged on the asset form for an asset type: ordered sections, each with ordered fields.</summary>
/// <remarks>Status (Active/Inactive) is kept in BaseEntity.Status.</remarks>
[BsonIgnoreExtraElements]
public sealed class AssetFieldLayout : BaseEntity
{
    [BsonElement("layout_name")]
    public string LayoutName { get; set; } = string.Empty;

    // Null means the layout is for all assets.
    [BsonElement("asset_type_id")]
    public string? AssetTypeId { get; set; }

    [BsonElement("asset_type_name")]
    public string? AssetTypeName { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("sections")]
    public List<AssetFieldLayoutSection> Sections { get; set; } = [];
}

/// <summary>A section of a layout, shown under a field group's name.</summary>
[BsonIgnoreExtraElements]
public sealed class AssetFieldLayoutSection
{
    [BsonElement("group_id")]
    public string GroupId { get; set; } = string.Empty;

    // Built-in asset field names (e.g. "asset_name") or custom field names, in display order.
    [BsonElement("field_names")]
    public List<string> FieldNames { get; set; } = [];
}
