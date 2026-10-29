using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class Site : BaseEntity
{
    [BsonElement("site_name")]
    public string SiteName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Both point at records in this service.
    [BsonElement("business_unit")]
    public MasterDataReference? BusinessUnit { get; set; }

    [BsonElement("department")]
    public MasterDataReference? Department { get; set; }

    [BsonElement("site_type")]
    public string? SiteType { get; set; }

    [BsonElement("city")]
    public string? City { get; set; }

    [BsonElement("country")]
    public string? Country { get; set; }
}
