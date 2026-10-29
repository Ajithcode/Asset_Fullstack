using MongoDB.Bson.Serialization.Attributes;

namespace A360.MasterDataManagement.Domain.ValueObjects;

/// <summary>
/// A link to another record, kept as id + name so it can be shown without a lookup.
/// The id is optional while the referenced master (organizations, departments, sites) does not exist yet.
/// </summary>
public class MasterDataReference
{
    [BsonElement("id")]
    public string? Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = null!;
}
