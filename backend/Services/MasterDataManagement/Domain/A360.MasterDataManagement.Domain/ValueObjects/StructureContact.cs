using MongoDB.Bson.Serialization.Attributes;

namespace A360.MasterDataManagement.Domain.ValueObjects;

/// <summary>
/// A named contact (organization contact, site manager, site emergency contact).
/// The phone number is kept without its dial code; PhoneCountry names the country whose code prefixes it.
/// </summary>
public class StructureContact
{
    [BsonElement("name")]
    public string? Name { get; set; }

    [BsonElement("designation")]
    public string? Designation { get; set; }

    [BsonElement("email")]
    public string? Email { get; set; }

    [BsonElement("phone_country")]
    public string? PhoneCountry { get; set; }

    [BsonElement("phone")]
    public string? Phone { get; set; }
}
