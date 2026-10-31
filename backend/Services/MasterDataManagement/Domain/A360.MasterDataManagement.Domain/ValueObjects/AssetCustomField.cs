using MongoDB.Bson.Serialization.Attributes;

namespace A360.MasterDataManagement.Domain.ValueObjects;

/// <summary>
/// An extra field captured on assets of a category or asset type, e.g. "Serial Number" (Text, required).
/// </summary>
public class AssetCustomField
{
    [BsonElement("name")]
    public string Name { get; set; } = null!;

    // Text, Number, Date, Dropdown or Yes / No.
    [BsonElement("type")]
    public string? Type { get; set; }

    [BsonElement("required")]
    public bool Required { get; set; }
}
