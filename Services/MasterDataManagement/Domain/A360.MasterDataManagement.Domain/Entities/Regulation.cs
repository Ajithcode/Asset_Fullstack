using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

public class Regulation : BaseEntity
{
    [BsonElement("regulation_name")]
    public string RegulationName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // e.g. "UAE", or "International" for cross-border rules.
    [BsonElement("country")]
    public string Country { get; set; } = null!;

    // e.g. "Safety" or "Environment".
    [BsonElement("category")]
    public string Category { get; set; } = null!;

    [BsonElement("issuing_authority")]
    public string? IssuingAuthority { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }
}
