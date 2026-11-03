using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>A model of an asset type (e.g. "C250 D6E" by Caterpillar for "Generator").</summary>
/// <remarks>Status (Active/Inactive) is kept in BaseEntity.Status.</remarks>
[BsonIgnoreExtraElements]
public sealed class AssetModel : BaseEntity
{
    [BsonElement("asset_type_id")]
    public string AssetTypeId { get; set; } = string.Empty;

    [BsonElement("model_name")]
    public string ModelName { get; set; } = string.Empty;

    [BsonElement("model_code")]
    public string ModelCode { get; set; } = string.Empty;

    // Manufacturers are kept in the Master Management service; the id links to it and the name is kept as selected.
    [BsonElement("manufacturer_id")]
    public string? ManufacturerId { get; set; }

    [BsonElement("manufacturer_name")]
    public string ManufacturerName { get; set; } = string.Empty;
}
