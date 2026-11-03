using MongoDB.Bson.Serialization.Attributes;

namespace A360.MasterDataManagement.Domain.ValueObjects;

/// <summary>
/// A break within a shift, e.g. lunch. The start is an optional 24-hour "HH:mm" time.
/// </summary>
public class ShiftBreak
{
    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("start")]
    public string? Start { get; set; }

    [BsonElement("minutes")]
    public int Minutes { get; set; }
}
