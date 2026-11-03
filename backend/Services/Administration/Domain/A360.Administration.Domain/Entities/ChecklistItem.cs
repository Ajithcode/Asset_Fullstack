using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class ChecklistItem : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("item_id")]
    public string ItemId { get; set; } = string.Empty;

    [BsonElement("item_text")]
    public string ItemText { get; set; } = string.Empty;

    // Section of the checklist the item belongs to, e.g. Electrical System.
    [BsonElement("section")]
    public string Section { get; set; } = string.Empty;

    // Pass/Fail, Yes/No, Numeric, Text or Photo.
    [BsonElement("response_type")]
    public string ResponseType { get; set; } = string.Empty;

    // Must be completed during execution.
    [BsonElement("is_required")]
    public bool IsRequired { get; set; }

    // Marks the item as critical for the overall result.
    [BsonElement("is_critical")]
    public bool IsCritical { get; set; }

    [BsonElement("help_text")]
    public string? HelpText { get; set; }

    // e.g. "OK / No corrosion" or a numeric range.
    [BsonElement("expected_value")]
    public string? ExpectedValue { get; set; }

    // Requires a photo or file attachment.
    [BsonElement("attachment_required")]
    public bool AttachmentRequired { get; set; }

    // Points awarded for a passing result.
    [BsonElement("score_value")]
    public int ScoreValue { get; set; }

    // e.g. Create Maintenance Issue.
    [BsonElement("failure_action")]
    public string? FailureAction { get; set; }

    // Automatically escalate to the responsible team on failure.
    [BsonElement("escalate_issue")]
    public bool EscalateIssue { get; set; }
}
