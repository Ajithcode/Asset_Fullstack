using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

// A priority or severity level used on work orders and issues.
public class PrioritySeverity : BaseEntity
{
    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Either "Priority" or "Severity".
    [BsonElement("type")]
    public string Type { get; set; } = null!;

    // Hex colour used to show the level, e.g. "#C62828".
    [BsonElement("color")]
    public string? Color { get; set; }

    // Expected response, e.g. "4 hours".
    [BsonElement("response_time")]
    public string? ResponseTime { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }
}
