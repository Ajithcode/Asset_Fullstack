using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

public class ServiceProvider : BaseEntity
{
    [BsonElement("service_provider_name")]
    public string ServiceProviderName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    [BsonElement("website")]
    public string? Website { get; set; }

    // Name of the main contact person.
    [BsonElement("primary_contact")]
    public string? PrimaryContact { get; set; }

    [BsonElement("service_type")]
    public string? ServiceType { get; set; }

    [BsonElement("email")]
    public string? Email { get; set; }

    [BsonElement("country")]
    public string? Country { get; set; }

    [BsonElement("phone")]
    public string? Phone { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }
}
