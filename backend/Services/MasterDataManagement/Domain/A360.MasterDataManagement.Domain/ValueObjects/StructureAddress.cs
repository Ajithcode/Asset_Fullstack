using MongoDB.Bson.Serialization.Attributes;

namespace A360.MasterDataManagement.Domain.ValueObjects;

/// <summary>
/// Postal address held by the organization structure masters (organizations, business units, sites, buildings).
/// </summary>
public class StructureAddress
{
    [BsonElement("line1")]
    public string? Line1 { get; set; }

    [BsonElement("line2")]
    public string? Line2 { get; set; }

    [BsonElement("city")]
    public string? City { get; set; }

    [BsonElement("state")]
    public string? State { get; set; }

    [BsonElement("country")]
    public string? Country { get; set; }

    [BsonElement("postal_code")]
    public string? PostalCode { get; set; }
}
