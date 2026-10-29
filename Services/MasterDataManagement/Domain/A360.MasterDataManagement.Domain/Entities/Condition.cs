using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

public class Condition : BaseEntity
{
    [BsonElement("condition_name")]
    public string ConditionName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    [BsonElement("type")]
    public string? Type { get; set; }

    // Hex colour used to show the condition, e.g. "#2E7D32".
    [BsonElement("color")]
    public string? Color { get; set; }

    // At most one condition per client is the default.
    [BsonElement("default_condition")]
    public bool DefaultCondition { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // 0 (worst) to 100 (best).
    [BsonElement("health_score")]
    public int? HealthScore { get; set; }
}
