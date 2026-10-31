using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Inspection.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class Priority : BaseEntity
{
    [BsonElement("priority_code")]
    public string PriorityCode { get; set; } = string.Empty;

    [BsonElement("priority_name")]
    public string PriorityName { get; set; } = string.Empty;

    [BsonElement("response_time")]
    public string ResponseTime { get; set; } = string.Empty;

    [BsonElement("completion_sla")]
    public string CompletionSla { get; set; } = string.Empty;

    [BsonElement("colour")]
    public string Colour { get; set; } = string.Empty;

    [BsonElement("escalation_rule")]
    public string EscalationRule { get; set; } = string.Empty;

    [BsonElement("is_active")]
    public bool IsActive { get; set; } = true;
}
