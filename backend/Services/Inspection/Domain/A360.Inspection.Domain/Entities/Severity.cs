using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Inspection.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class Severity : BaseEntity
{
    [BsonElement("severity_code")]
    public string SeverityCode { get; set; } = string.Empty;

    [BsonElement("severity_name")]
    public string SeverityName { get; set; } = string.Empty;

    [BsonElement("score")]
    public int Score { get; set; }

    [BsonElement("colour_indicator")]
    public string ColourIndicator { get; set; } = string.Empty;

    [BsonElement("resolution_sla")]
    public string ResolutionSla { get; set; } = string.Empty;

    [BsonElement("escalation_level")]
    public string EscalationLevel { get; set; } = string.Empty;

    [BsonElement("is_active")]
    public bool IsActive { get; set; } = true;
}
