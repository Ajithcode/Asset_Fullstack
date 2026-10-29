using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

// A manufacturer's models are not stored here: each asset model points at its manufacturer.
public class Manufacturer : BaseEntity
{
    [BsonElement("manufacturer_name")]
    public string ManufacturerName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    [BsonElement("country")]
    public string? Country { get; set; }

    [BsonElement("website")]
    public string? Website { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("contact_person")]
    public string? ContactPerson { get; set; }

    [BsonElement("email")]
    public string? Email { get; set; }

    [BsonElement("phone_number")]
    public string? PhoneNumber { get; set; }
}
