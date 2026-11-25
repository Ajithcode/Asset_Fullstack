using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>A criticality assessment of one registered asset, for the Criticality tab and the Asset Criticality List.</summary>
/// <remarks>
/// Each assess / re-assess saves a new record; IsCurrent marks the latest one for the asset, and the older ones
/// are its Review History. Score and level are worked out from the criteria when saved.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class AssetCriticalityAssessment : BaseEntity
{
    [BsonElement("asset_registration_id")]
    public string AssetRegistrationId { get; set; } = string.Empty;

    // Copied from the asset when the assessment is saved, so the list can be searched and filtered without loading assets.
    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("asset_tag")]
    public string? AssetTag { get; set; }

    [BsonElement("category")]
    public string? Category { get; set; }

    [BsonElement("location")]
    public string? Location { get; set; }

    [BsonElement("asset_status")]
    public string? AssetStatus { get; set; }

    [BsonElement("is_current")]
    public bool IsCurrent { get; set; }

    [BsonElement("assessment_date")]
    public DateTime AssessmentDate { get; set; }

    [BsonElement("valid_until")]
    public DateTime? ValidUntil { get; set; }

    [BsonElement("next_review_date")]
    public DateTime? NextReviewDate { get; set; }

    [BsonElement("assessed_by")]
    public string AssessedBy { get; set; } = string.Empty;

    [BsonElement("approved_by")]
    public string ApprovedBy { get; set; } = string.Empty;

    // "Very Low", "Low", "Medium", "High" or "Very High"; the Criticality Matrix position.
    [BsonElement("likelihood")]
    public string? Likelihood { get; set; }

    [BsonElement("consequence")]
    public string? Consequence { get; set; }

    [BsonElement("criteria")]
    public List<AssetCriticalityCriterion> Criteria { get; set; } = [];

    // Sum of the weighted criteria, 0 to 100.
    [BsonElement("criticality_score")]
    public decimal CriticalityScore { get; set; }

    // "Low", "Medium", "High" or "Critical", from the score.
    [BsonElement("criticality_level")]
    public string CriticalityLevel { get; set; } = string.Empty;

    [BsonElement("remarks")]
    public string? Remarks { get; set; }

    // Additional Information panel; carried over to the next assessment unless changed.
    [BsonElement("assessment_method")]
    public string? AssessmentMethod { get; set; }

    // e.g. "Every 6 Months".
    [BsonElement("review_frequency")]
    public string? ReviewFrequency { get; set; }

    [BsonElement("department")]
    public string? Department { get; set; }

    [BsonElement("asset_group")]
    public string? AssetGroup { get; set; }

    // Workflow state, see AssetCriticalityApprovalStatuses. Records saved before the approval workflow have none and
    // count as Approved (they were published when saved).
    [BsonElement("approval_status")]
    public string? ApprovalStatus { get; set; }

    // "Initial" or "Reassessment".
    [BsonElement("assessment_type")]
    public string? AssessmentType { get; set; }

    // Template the ratings were made with; "LEGACY" for assessments saved before templates existed.
    [BsonElement("template_code")]
    public string? TemplateCode { get; set; }

    // ----- Phase 3: scoring, workflow ------------------------------------------------------------------------------

    // ACA-2026-000123
    [BsonElement("assessment_number")]
    public string? AssessmentNumber { get; set; }

    // Template version the ratings were scored with (weights come from it, never from the client).
    [BsonElement("template_id")]
    public string? TemplateId { get; set; }

    [BsonElement("template_name")]
    public string? TemplateName { get; set; }

    [BsonElement("template_version")]
    public int? TemplateVersion { get; set; }

    // Level the score gives (CriticalityLevel is the final level, after an override).
    [BsonElement("calculated_level")]
    public string? CalculatedLevel { get; set; }

    [BsonElement("criticality_level_id")]
    public string? CriticalityLevelId { get; set; }

    [BsonElement("criticality_level_code")]
    public string? CriticalityLevelCode { get; set; }

    [BsonElement("is_override")]
    public bool IsOverride { get; set; }

    // Required for an override and when the level is lowered.
    [BsonElement("justification")]
    public string? Justification { get; set; }

    [BsonElement("previous_approved_assessment_id")]
    public string? PreviousApprovedAssessmentId { get; set; }

    [BsonElement("previous_level")]
    public string? PreviousLevel { get; set; }

    [BsonElement("risk")]
    public AssetCriticalityRisk Risk { get; set; } = new();

    [BsonElement("attachments")]
    public List<AssetCriticalityAttachment> Attachments { get; set; } = [];

    [BsonElement("assessed_by_user_id")]
    public string? AssessedByUserId { get; set; }

    [BsonElement("created_by_user_id")]
    public string? CreatedByUserId { get; set; }

    [BsonElement("submitted_by")]
    public string? SubmittedBy { get; set; }

    [BsonElement("submitted_by_user_id")]
    public string? SubmittedByUserId { get; set; }

    [BsonElement("submitted_at")]
    public DateTime? SubmittedAt { get; set; }

    [BsonElement("approved_by_user_id")]
    public string? ApprovedByUserId { get; set; }

    [BsonElement("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [BsonElement("rejected_reason")]
    public string? RejectedReason { get; set; }

    // Approval rule that applied when submitted (information only; permissions decide who may approve).
    [BsonElement("approval_scenario")]
    public string? ApprovalScenario { get; set; }

    [BsonElement("approver_roles")]
    public List<string> ApproverRoles { get; set; } = [];

    [BsonElement("approval_type")]
    public string? ApprovalType { get; set; }

    [BsonElement("trail")]
    public List<AssetCriticalityTrailEntry> Trail { get; set; } = [];

    // Bumped on every save; edits and decisions must match the version they were read at.
    [BsonElement("concurrency_version")]
    public int ConcurrencyVersion { get; set; } = 1;
}

[BsonIgnoreExtraElements]
public sealed class AssetCriticalityRisk
{
    [BsonElement("failure_consequences")] public string? FailureConsequences { get; set; }
    [BsonElement("alternative_equipment_available")] public bool? AlternativeEquipmentAvailable { get; set; }
    [BsonElement("downtime_impact")] public string? DowntimeImpact { get; set; }
    [BsonElement("financial_loss")] public string? FinancialLoss { get; set; }
    [BsonElement("safety_environmental_consequences")] public string? SafetyEnvironmentalConsequences { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class AssetCriticalityAttachment
{
    [BsonElement("file_name")] public string FileName { get; set; } = string.Empty;
    [BsonElement("file_url")] public string FileUrl { get; set; } = string.Empty;
    [BsonElement("file_type")] public string? FileType { get; set; }
    [BsonElement("file_size")] public long FileSize { get; set; }
    [BsonElement("uploaded_by")] public string? UploadedBy { get; set; }
    [BsonElement("uploaded_at")] public DateTime? UploadedAt { get; set; }
}

/// <summary>One step of the approval trail: Created, Saved, Submitted, Approved, Rejected, Cancelled.</summary>
[BsonIgnoreExtraElements]
public sealed class AssetCriticalityTrailEntry
{
    [BsonElement("action")] public string Action { get; set; } = string.Empty;
    [BsonElement("by")] public string? By { get; set; }
    [BsonElement("by_user_id")] public string? ByUserId { get; set; }
    [BsonElement("at")] public DateTime At { get; set; }
    [BsonElement("comments")] public string? Comments { get; set; }
}

/// <summary>Approved classification changes of an asset (written when an assessment is approved).</summary>
[BsonIgnoreExtraElements]
public sealed class AssetCriticalityHistory : BaseEntity
{
    [BsonElement("asset_registration_id")] public string AssetRegistrationId { get; set; } = string.Empty;
    [BsonElement("assessment_id")] public string AssessmentId { get; set; } = string.Empty;
    [BsonElement("from_level")] public string? FromLevel { get; set; }
    [BsonElement("to_level")] public string ToLevel { get; set; } = string.Empty;
    [BsonElement("to_level_id")] public string? ToLevelId { get; set; }
    [BsonElement("score")] public decimal Score { get; set; }
    [BsonElement("effective_from")] public DateTime EffectiveFrom { get; set; }
    [BsonElement("approved_by")] public string? ApprovedBy { get; set; }
    [BsonElement("change_reason")] public string? ChangeReason { get; set; }
}

/// <summary>Workflow states of a criticality assessment.</summary>
public static class AssetCriticalityApprovalStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";

    /// <summary>Shown on the landing page for assets without an approved assessment.</summary>
    public const string Unassessed = "Unassessed";

    /// <summary>Assessments still being worked on or waiting for a decision.</summary>
    public static readonly string[] Open = [Draft, Submitted, PendingApproval, Rejected];

    /// <summary>Waiting for an approver (Pending Approvals KPI).</summary>
    public static readonly string[] AwaitingApproval = [Submitted, PendingApproval];

    public static readonly string[] All = [Draft, Submitted, PendingApproval, Approved, Rejected, Cancelled];
}

public static class AssetCriticalityAssessmentTypes
{
    public const string Initial = "Initial";
    public const string Reassessment = "Reassessment";
}

/// <summary>One row of the criteria table, e.g. Operational Impact, weight 25, score 5, weighted 25.0.</summary>
public sealed class AssetCriticalityCriterion
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    // Percent; the weights of one assessment add up to 100.
    [BsonElement("weight")]
    public decimal Weight { get; set; }

    // 1 to 5 in steps of 0.5.
    [BsonElement("score")]
    public decimal Score { get; set; }

    // Weight x Score / 5.
    [BsonElement("weighted_score")]
    public decimal WeightedScore { get; set; }

    [BsonElement("justification")]
    public string Justification { get; set; } = string.Empty;

    // Phase 3: set by the template-based form; older rows have none. Score = Rating for these rows.
    [BsonElement("criterion_id")]
    public string? CriterionId { get; set; }

    [BsonElement("code")]
    public string? Code { get; set; }

    [BsonElement("rating")]
    public int? Rating { get; set; }
}
