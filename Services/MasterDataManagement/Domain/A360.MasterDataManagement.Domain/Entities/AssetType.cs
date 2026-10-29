using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class AssetType : BaseEntity
{
    [BsonElement("asset_type_name")]
    public string AssetTypeName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Points at an asset category in this service.
    [BsonElement("category")]
    public MasterDataReference? Category { get; set; }

    [BsonElement("asset_class")]
    public string? AssetClass { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("depreciation_method")]
    public string? DepreciationMethod { get; set; }

    // Useful life in years.
    [BsonElement("default_lifetime")]
    public int? DefaultLifetime { get; set; }
}
