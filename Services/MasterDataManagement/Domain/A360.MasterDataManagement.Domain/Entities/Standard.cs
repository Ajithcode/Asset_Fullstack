using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

public class Standard : BaseEntity
{
    [BsonElement("standard_name")]
    public string StandardName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // The standards body, e.g. "ISO" or "NFPA" (not an Organization record).
    [BsonElement("organization")]
    public string Organization { get; set; } = null!;

    // e.g. "Quality Management" or "Electrical".
    [BsonElement("category")]
    public string Category { get; set; } = null!;

    // Edition or year, e.g. "2015".
    [BsonElement("version")]
    public string? Version { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }
}
