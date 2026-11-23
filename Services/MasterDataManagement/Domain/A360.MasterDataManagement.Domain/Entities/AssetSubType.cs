using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

/// <summary>A sub type of an asset type (e.g. Moulded Case Circuit Breaker under Circuit Breaker).</summary>
public class AssetSubType : BaseEntity
{
    [BsonElement("sub_type_name")]
    public string SubTypeName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Points at an asset type in this service.
    [BsonElement("asset_type")]
    public MasterDataReference? AssetType { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // Page-owned settings (icon, tagging, lifecycle, defaults, specifications, custom fields) kept as one JSON document.
    [BsonElement("settings_json")]
    public string? SettingsJson { get; set; }
}
