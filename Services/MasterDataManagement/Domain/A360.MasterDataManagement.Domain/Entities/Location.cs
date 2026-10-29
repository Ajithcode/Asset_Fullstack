using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

public class Location : BaseEntity
{
    [BsonElement("location_name")]
    public string LocationName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    [BsonElement("type")]
    public string Type { get; set; } = null!;

    [BsonElement("building")]
    public string? Building { get; set; }

    [BsonElement("floor")]
    public string? Floor { get; set; }

    [BsonElement("zone")]
    public string? Zone { get; set; }

    [BsonElement("parent_location_id")]
    public string? ParentLocationId { get; set; }

    // Denormalized so lists can show the parent without a second lookup; kept in sync on rename.
    [BsonElement("parent_location_name")]
    public string? ParentLocationName { get; set; }
}
