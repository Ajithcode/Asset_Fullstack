using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>An audit that verifies the existence, condition and compliance of a set of assets, from the New Asset Audit wizard.</summary>
/// <remarks>
/// Holds all five steps: basic information and scope, the selected assets, the criteria, the team and schedule,
/// and the attachments and notes. Status is BaseEntity.Status: Draft until submitted, then Scheduled, In Progress
/// and Completed ("Overdue" is worked out from the end date, not stored).
/// Asset and person names are copied when saved, so the audit can be shown without loading them.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class AssetAudit : BaseEntity
{
    // e.g. "AUD-2025-025"; generated when left empty.
    [BsonElement("audit_reference")]
    public string AuditReference { get; set; } = string.Empty;

    // Furthest step saved so far, 1 to 5, so the wizard can resume there.
    [BsonElement("current_step")]
    public int CurrentStep { get; set; }

    // Step 1: Basic Information.
    [BsonElement("audit_name")]
    public string AuditName { get; set; } = string.Empty;

    // e.g. "Periodic", "Ad-hoc", "Compliance".
    [BsonElement("audit_type")]
    public string? AuditType { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("scope")]
    public AssetAuditScope Scope { get; set; } = new();

    // The Audit Criteria checkboxes on step 1, e.g. "PhysicalExistence"; step 3 holds the full criteria.
    [BsonElement("criteria_options")]
    public List<string> CriteriaOptions { get; set; } = [];

    [BsonElement("other_criteria_details")]
    public string? OtherCriteriaDetails { get; set; }

    // Step 2: the assets picked on Select Assets & Sensors. When the selection method is not "Selected Assets",
    // the assets in scope are added to these on submit.
    [BsonElement("assets")]
    public List<AssetAuditAsset> Assets { get; set; } = [];

    // Step 3: Audit Criteria.
    [BsonElement("criteria")]
    public List<AssetAuditCriterion> Criteria { get; set; } = [];

    [BsonElement("criteria_settings")]
    public AssetAuditCriteriaSettings CriteriaSettings { get; set; } = new();

    // Step 4: Assign & Schedule.
    [BsonElement("team")]
    public List<AssetAuditTeamMember> Team { get; set; } = [];

    [BsonElement("schedule")]
    public AssetAuditSchedule Schedule { get; set; } = new();

    [BsonElement("plan")]
    public List<AssetAuditPlanEntry> Plan { get; set; } = [];

    [BsonElement("notifications")]
    public AssetAuditNotifications Notifications { get; set; } = new();

    // Step 5: Review & Submit.
    [BsonElement("attachments")]
    public List<AssetAuditAttachment> Attachments { get; set; } = [];

    [BsonElement("notes")]
    public string? Notes { get; set; }

    [BsonElement("submitted_by")]
    public string? SubmittedBy { get; set; }

    [BsonElement("submitted_at")]
    public DateTime? SubmittedAt { get; set; }

    // Carrying out the audit.
    [BsonElement("started_at")]
    public DateTime? StartedAt { get; set; }

    [BsonElement("completed_at")]
    public DateTime? CompletedAt { get; set; }

    // The Findings column, e.g. 2 discrepancies.
    [BsonElement("discrepancy_count")]
    public int? DiscrepancyCount { get; set; }

    [BsonElement("completion_remarks")]
    public string? CompletionRemarks { get; set; }
}

/// <summary>Which assets the audit covers.</summary>
public sealed class AssetAuditScope
{
    // Any level of the location hierarchy (site, building, floor…); assets at or below it are in scope.
    [BsonElement("location_id")]
    public string? LocationId { get; set; }

    // e.g. "Main Plant - Block A".
    [BsonElement("location_name")]
    public string? LocationName { get; set; }

    // Null means all departments.
    [BsonElement("department_id")]
    public string? DepartmentId { get; set; }

    [BsonElement("department_name")]
    public string? DepartmentName { get; set; }

    // Null means all categories.
    [BsonElement("category_id")]
    public string? CategoryId { get; set; }

    [BsonElement("category_name")]
    public string? CategoryName { get; set; }

    // "All In Location", "Selected Assets", "Tag Range" or "Filter".
    [BsonElement("selection_method")]
    public string? SelectionMethod { get; set; }

    // "By asset tag range", e.g. "GEN-00001" to "GEN-00100".
    [BsonElement("tag_from")]
    public string? TagFrom { get; set; }

    [BsonElement("tag_to")]
    public string? TagTo { get; set; }

    // "By filter / criteria".
    [BsonElement("asset_status")]
    public string? AssetStatus { get; set; }

    [BsonElement("asset_type")]
    public string? AssetType { get; set; }

    [BsonElement("criticality")]
    public string? Criticality { get; set; }

    // Counted when the scope is saved, for the Audit Summary panel.
    [BsonElement("total_assets_in_scope")]
    public int TotalAssetsInScope { get; set; }
}

/// <summary>An asset in the audit, as it was when picked, with the sensors / components to check.</summary>
public sealed class AssetAuditAsset
{
    [BsonElement("asset_registration_id")]
    public string AssetRegistrationId { get; set; } = string.Empty;

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("asset_tag")]
    public string? AssetTag { get; set; }

    [BsonElement("category")]
    public string? Category { get; set; }

    [BsonElement("asset_type")]
    public string? AssetType { get; set; }

    [BsonElement("location")]
    public string? Location { get; set; }

    [BsonElement("status")]
    public string? Status { get; set; }

    // AssetComponent ids; empty means none are checked separately.
    [BsonElement("component_ids")]
    public List<string> ComponentIds { get; set; } = [];
}

public sealed class AssetAuditCriterion
{
    // Stable within the audit, e.g. "crt-1"; used by Applies To and by audit results.
    [BsonElement("criterion_id")]
    public string CriterionId { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; }

    // e.g. "Existence", "Identification", "Location", "Condition", "Components", "Compliance".
    [BsonElement("category")]
    public string? Category { get; set; }

    // The tabs: "Checklist", "Condition Assessment", "Compliance", "Documentation" or "Custom".
    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    // e.g. "Visual Inspection", "Scan Barcode / RFID", "Compare with record".
    [BsonElement("check_method")]
    public string? CheckMethod { get; set; }

    // "All Assets" or "Selected Assets".
    [BsonElement("applies_to")]
    public string AppliesTo { get; set; } = string.Empty;

    // Only for "Selected Assets".
    [BsonElement("asset_registration_ids")]
    public List<string> AssetRegistrationIds { get; set; } = [];

    [BsonElement("is_mandatory")]
    public bool IsMandatory { get; set; }
}

public sealed class AssetAuditCriteriaSettings
{
    [BsonElement("capture_photos")]
    public bool CapturePhotos { get; set; }

    [BsonElement("allow_comments")]
    public bool AllowComments { get; set; }

    // New criteria start as mandatory.
    [BsonElement("default_mandatory")]
    public bool DefaultMandatory { get; set; }

    [BsonElement("show_pass_fail")]
    public bool ShowPassFail { get; set; }

    // Score each criterion 0 to 100.
    [BsonElement("enable_scoring")]
    public bool EnableScoring { get; set; }

    [BsonElement("allow_attachments")]
    public bool AllowAttachments { get; set; }
}

public sealed class AssetAuditTeamMember
{
    [BsonElement("user_id")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("email")]
    public string? Email { get; set; }

    // "Lead Auditor", "Auditor", "Observer" or "Reviewer".
    [BsonElement("role")]
    public string Role { get; set; } = string.Empty;

    [BsonElement("department")]
    public string? Department { get; set; }

    // Worked out from the plan.
    [BsonElement("assigned_assets")]
    public int AssignedAssets { get; set; }
}

public sealed class AssetAuditSchedule
{
    [BsonElement("start_date")]
    public DateTime? StartDate { get; set; }

    [BsonElement("end_date")]
    public DateTime? EndDate { get; set; }

    // "Weekdays (Mon - Fri)", "All Days" or "Custom".
    [BsonElement("working_days")]
    public string? WorkingDays { get; set; }

    // For "Custom", e.g. ["Sunday", "Monday"].
    [BsonElement("custom_days")]
    public List<string> CustomDays { get; set; } = [];

    // "HH:mm".
    [BsonElement("daily_start_time")]
    public string? DailyStartTime { get; set; }

    [BsonElement("daily_end_time")]
    public string? DailyEndTime { get; set; }

    // "By Location", "By Asset Category" or "Custom Order".
    [BsonElement("location_sequence")]
    public string? LocationSequence { get; set; }

    // The plan is generated by the server; false when it was edited by hand.
    [BsonElement("auto_plan")]
    public bool AutoPlan { get; set; } = true;
}

/// <summary>One line of the Audit Plan: a day's visit to one location by one auditor.</summary>
public sealed class AssetAuditPlanEntry
{
    [BsonElement("date")]
    public DateTime Date { get; set; }

    // The location or category the line covers, by the schedule's sequence.
    [BsonElement("location")]
    public string? Location { get; set; }

    [BsonElement("asset_registration_ids")]
    public List<string> AssetRegistrationIds { get; set; } = [];

    [BsonElement("assigned_to_user_id")]
    public string? AssignedToUserId { get; set; }

    [BsonElement("assigned_to_name")]
    public string? AssignedToName { get; set; }

    // "Scheduled", "In Progress" or "Completed".
    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;
}

public sealed class AssetAuditNotifications
{
    [BsonElement("notify_team_on_assignment")]
    public bool NotifyTeamOnAssignment { get; set; }

    // One day before each planned day.
    [BsonElement("send_schedule_reminder")]
    public bool SendScheduleReminder { get; set; }

    [BsonElement("notify_on_completion")]
    public bool NotifyOnCompletion { get; set; }

    [BsonElement("notify_on_schedule_change")]
    public bool NotifyOnScheduleChange { get; set; }
}

/// <summary>A file uploaded through the Media service; this keeps its link and details.</summary>
public sealed class AssetAuditAttachment
{
    [BsonElement("file_name")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("file_url")]
    public string FileUrl { get; set; } = string.Empty;

    // e.g. "Template", "Image", "Document".
    [BsonElement("file_type")]
    public string? FileType { get; set; }

    [BsonElement("content_type")]
    public string? ContentType { get; set; }

    [BsonElement("file_size")]
    public long? FileSize { get; set; }

    [BsonElement("uploaded_by")]
    public string? UploadedBy { get; set; }

    [BsonElement("uploaded_at")]
    public DateTime? UploadedAt { get; set; }
}
