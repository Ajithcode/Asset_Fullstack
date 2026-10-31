
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Wip.Domain.Entities;

public class SlaMaster : BaseEntity
{
    [BsonElement("sla_id")]
    public string SlaId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string? AssetId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("sla_name")]
    public string SlaName { get; set; } = null!;

    [BsonElement("work_type")]
    public string? WorkType { get; set; }

    [BsonElement("priority")]
    public string? Priority { get; set; }

    [BsonElement("response_time_minutes")]
    public int? ResponseTimeMinutes { get; set; }

    [BsonElement("resolution_time_minutes")]
    public int? ResolutionTimeMinutes { get; set; }

    [BsonElement("escalation_level_1")]
    public string? EscalationLevel1 { get; set; }

    [BsonElement("escalation_level_2")]
    public string? EscalationLevel2 { get; set; }

    [BsonElement("escalation_level_3")]
    public string? EscalationLevel3 { get; set; }
}
