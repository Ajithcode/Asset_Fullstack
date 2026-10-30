
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Wip.Domain.Entities;

public class AlertMaster : BaseEntity
{
    [BsonElement("alert_id")]
    public string AlertId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string? AssetId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("alert_type")]
    public string? AlertType { get; set; }

    [BsonElement("trigger_condition")]
    public string? TriggerCondition { get; set; }

    [BsonElement("notification_channel")]
    public string? NotificationChannel { get; set; }

    [BsonElement("recipient_role")]
    public string? RecipientRole { get; set; }

    [BsonElement("escalation_level")]
    public string? EscalationLevel { get; set; }
}
