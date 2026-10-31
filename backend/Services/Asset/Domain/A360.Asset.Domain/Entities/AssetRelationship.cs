using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>A main asset and the sub assets linked to it, for the Asset Relationship screen.</summary>
/// <remarks>
/// One record per main asset. Names, category and location are copied when saved, so the list can be
/// searched and shown without loading assets. Created by / last updated are BaseEntity fields.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class AssetRelationship : BaseEntity
{
    [BsonElement("main_asset_registration_id")]
    public string MainAssetRegistrationId { get; set; } = string.Empty;

    [BsonElement("main_asset_name")]
    public string? MainAssetName { get; set; }

    [BsonElement("main_asset_tag")]
    public string? MainAssetTag { get; set; }

    [BsonElement("main_category")]
    public string? MainCategory { get; set; }

    [BsonElement("main_location")]
    public string? MainLocation { get; set; }

    [BsonElement("sub_assets")]
    public List<AssetRelationshipSubAsset> SubAssets { get; set; } = [];
}

/// <summary>One row of the Related Assets table: a registered asset or a component of the main asset.</summary>
public sealed class AssetRelationshipSubAsset
{
    // Set for a registered asset (Connected Asset, Supporting Asset, Sub Asset).
    [BsonElement("asset_registration_id")]
    public string? AssetRegistrationId { get; set; }

    // Set for a component of the main asset (Component).
    [BsonElement("component_id")]
    public string? ComponentId { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    // Asset tag, or component tag for a component.
    [BsonElement("tag")]
    public string? Tag { get; set; }

    // "Connected Asset", "Supporting Asset", "Component" or "Sub Asset".
    [BsonElement("relationship_type")]
    public string RelationshipType { get; set; } = string.Empty;

    [BsonElement("category")]
    public string? Category { get; set; }

    [BsonElement("location")]
    public string? Location { get; set; }
}
