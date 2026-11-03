using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Inspection.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class FailureReason : BaseEntity
{
    [BsonElement("failure_reason_code")]
    public string FailureReasonCode { get; set; } = string.Empty;

    [BsonElement("failure_reason_name")]
    public string FailureReasonName { get; set; } = string.Empty;

    [BsonElement("failure_category")]
    public string FailureCategory { get; set; } = string.Empty;

    [BsonElement("severity")]
    public string Severity { get; set; } = string.Empty;

    [BsonElement("is_corrective_action_required")]
    public bool IsCorrectiveActionRequired { get; set; }

    [BsonElement("is_escalation_required")]
    public bool IsEscalationRequired { get; set; }

    [BsonElement("default_responsible_team")]
    public string DefaultResponsibleTeam { get; set; } = string.Empty;

    [BsonElement("is_active")]
    public bool IsActive { get; set; } = true;
}
