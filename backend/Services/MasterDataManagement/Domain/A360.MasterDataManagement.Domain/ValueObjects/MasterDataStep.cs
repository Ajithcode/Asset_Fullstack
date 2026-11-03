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

    // Time the step takes, in minutes (task template tasks).
    [BsonElement("duration_minutes")]
    public int? DurationMinutes { get; set; }

    // How the step is answered, e.g. "Pass / Fail" or "Number" (checklist items).
    [BsonElement("response_type")]
    public string? ResponseType { get; set; }

    [BsonElement("is_required")]
    public bool IsRequired { get; set; }
}
