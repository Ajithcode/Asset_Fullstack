using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class AssetModel : BaseEntity
{
    [BsonElement("model_name")]
    public string ModelName { get; set; } = null!;

    [BsonElement("model_code")]
    public string ModelCode { get; set; } = null!;

    // Manufacturer, asset type and category all point at records in this service.
    [BsonElement("manufacturer")]
    public MasterDataReference? Manufacturer { get; set; }

    [BsonElement("asset_type")]
    public MasterDataReference? AssetType { get; set; }

    [BsonElement("category")]
    public MasterDataReference? Category { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("lifecycle")]
    public string? Lifecycle { get; set; }
}
