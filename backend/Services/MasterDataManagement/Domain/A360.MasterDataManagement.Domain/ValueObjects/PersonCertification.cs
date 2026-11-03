using MongoDB.Bson.Serialization.Attributes;

namespace A360.MasterDataManagement.Domain.ValueObjects;

/// <summary>
/// A certificate held by an employee or technician. No expiry means it does not expire.
/// </summary>
public class PersonCertification
{
    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("expiry")]
    public DateOnly? Expiry { get; set; }
}
