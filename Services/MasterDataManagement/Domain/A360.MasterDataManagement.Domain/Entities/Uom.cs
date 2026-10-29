using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

// A unit of measure, e.g. Kilogram (KG, kg).
public class Uom : BaseEntity
{
    [BsonElement("uom_name")]
    public string UomName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // e.g. "Weight", "Volume" or "Discrete".
    [BsonElement("type")]
    public string Type { get; set; } = null!;

    // How quantities are written, e.g. "kg" or "m²".
    [BsonElement("symbol")]
    public string Symbol { get; set; } = null!;

    // How many base units of the same type one of this unit is, e.g. 1000 for a gram base.
    [BsonElement("conversion_factor")]
    public double? ConversionFactor { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }
}
