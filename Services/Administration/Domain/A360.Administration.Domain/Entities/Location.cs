using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class Location : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [BsonElement("location_code")]
    public string LocationCode { get; set; } = string.Empty;

    [BsonElement("location_name")]
    public string LocationName { get; set; } = string.Empty;

    // Organization, Region, Country, Site, Building, Floor, Warehouse, ...
    [BsonElement("location_type")]
    public string LocationType { get; set; } = string.Empty;

    // LocationId of the parent; null only for the top-level organization node.
    [BsonElement("parent_location_id")]
    public string? ParentLocationId { get; set; }

    [BsonElement("address")]
    public string? Address { get; set; }

    [BsonElement("country")]
    public string Country { get; set; } = string.Empty;

    [BsonElement("city")]
    public string City { get; set; } = string.Empty;

    [BsonElement("time_zone")]
    public string TimeZone { get; set; } = string.Empty;

    [BsonElement("location_manager")]
    public string LocationManager { get; set; } = string.Empty;
}
