using MongoDB.Bson.Serialization.Attributes;

namespace A360.MasterDataManagement.Domain.ValueObjects;

/// <summary>
/// A paper kept on a vendor (trade licence, insurance, contract...). No expiry means it does not expire.
/// </summary>
public class VendorDocument
{
    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("type")]
    public string? Type { get; set; }

    [BsonElement("expiry")]
    public DateOnly? Expiry { get; set; }
}
