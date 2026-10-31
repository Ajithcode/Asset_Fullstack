using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Inspection.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class InspectionType : BaseEntity
{
    [BsonElement("inspection_type_code")]
    public string InspectionTypeCode { get; set; } = string.Empty;

    [BsonElement("asset_id")]
    public string AssetId { get; set; } = string.Empty;

    [BsonElement("asset_name")]
    public string AssetName { get; set; } = string.Empty;

    [BsonElement("inspection_type_name")]
    public string InspectionTypeName { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("default_priority")]
    public string DefaultPriority { get; set; } = string.Empty;

    [BsonElement("default_approval_workflow")]
    public string DefaultApprovalWorkflow { get; set; } = string.Empty;

    [BsonElement("default_report_template")]
    public string DefaultReportTemplate { get; set; } = string.Empty;

    [BsonElement("is_active")]
    public bool IsActive { get; set; } = true;

    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;

    [BsonElement("default_checklist_code")]
    public string DefaultChecklistCode { get; set; } = string.Empty;

    [BsonElement("default_checklist_name")]
    public string DefaultChecklistName { get; set; } = string.Empty;

    [BsonElement("default_frequency")]
    public string DefaultFrequency { get; set; } = string.Empty;

    [BsonElement("applicable_assets")]
    public string ApplicableAssets { get; set; } = string.Empty;

    [BsonElement("responsible_role")]
    public string ResponsibleRole { get; set; } = string.Empty;

    [BsonElement("photo_evidence_on_failure")]
    public bool PhotoEvidenceOnFailure { get; set; }

    [BsonElement("signature_required")]
    public bool SignatureRequired { get; set; }

    [BsonElement("corrective_action_on_failure")]
    public bool CorrectiveActionOnFailure { get; set; }

    [BsonElement("permit_required")]
    public bool PermitRequired { get; set; }
}
