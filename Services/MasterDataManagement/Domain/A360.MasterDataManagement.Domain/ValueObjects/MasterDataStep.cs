using MongoDB.Bson.Serialization.Attributes;

namespace A360.MasterDataManagement.Domain.ValueObjects;

/// <summary>
/// One entry in an ordered list of steps, such as a task in a task template or an item in a checklist.
/// </summary>
public class MasterDataStep
{
    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("description")]
    public string? Description { get; set; }
}
