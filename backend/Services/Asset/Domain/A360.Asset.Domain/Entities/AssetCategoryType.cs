using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>An asset type within an asset category (e.g. "Generator" under "Power Generation"); models belong to a type.</summary>
/// <remarks>Status (Active/Inactive) is kept in BaseEntity.Status.</remarks>
[BsonIgnoreExtraElements]
public sealed class AssetCategoryType : BaseEntity
{
    [BsonElement("category_id")]
    public string CategoryId { get; set; } = string.Empty;

    [BsonElement("type_name")]
    public string TypeName { get; set; } = string.Empty;

    [BsonElement("type_code")]
    public string TypeCode { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; }

    // URL returned by the Media service upload endpoint.
    [BsonElement("image_url")]
    public string? ImageUrl { get; set; }
}
