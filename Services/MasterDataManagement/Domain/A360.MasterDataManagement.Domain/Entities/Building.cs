using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class Building : BaseEntity
{
    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Points at a site in this service.
    [BsonElement("site")]
    public MasterDataReference? Site { get; set; }

    [BsonElement("parent_level")]
    public string? ParentLevel { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("capacity")]
    public int? Capacity { get; set; }

    [BsonElement("area")]
    public double? Area { get; set; }
}
