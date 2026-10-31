using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Inspection.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class InspectionChecklistResponse
{
    [BsonElement("section_name")]
    public string SectionName { get; set; } = string.Empty;

    [BsonElement("task_code")]
    public string TaskCode { get; set; } = string.Empty;

    [BsonElement("task_title")]
    public string TaskTitle { get; set; } = string.Empty;

    [BsonElement("response_type")]
    public string ResponseType { get; set; } = string.Empty;

    [BsonElement("unit")]
    public string Unit { get; set; } = string.Empty;

    [BsonElement("min_value")]
    public double? MinValue { get; set; }

    [BsonElement("max_value")]
    public double? MaxValue { get; set; }

    [BsonElement("procedure")]
    public string Procedure { get; set; } = string.Empty;

    [BsonElement("required")]
    public bool Required { get; set; }

    [BsonElement("reading")]
    public string Reading { get; set; } = string.Empty;

    [BsonElement("result")]
    public string Result { get; set; } = string.Empty;

    [BsonElement("notes")]
    public string Notes { get; set; } = string.Empty;

    [BsonElement("evidence")]
    public List<string> Evidence { get; set; } = [];
}

[BsonIgnoreExtraElements]
public sealed class SignOffStep
{
    [BsonElement("role")]
    public string Role { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("state")]
    public string State { get; set; } = string.Empty;

    [BsonElement("signed_at")]
    public string? SignedAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class Inspection : BaseEntity
{
    [BsonElement("inspection_code")]
    public string InspectionCode { get; set; } = string.Empty;

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("inspection_type_name")]
    public string InspectionTypeName { get; set; } = string.Empty;

    [BsonElement("type_short")]
    public string TypeShort { get; set; } = string.Empty;

    [BsonElement("priority")]
    public string Priority { get; set; } = string.Empty;

    [BsonElement("site_name")]
    public string SiteName { get; set; } = string.Empty;

    [BsonElement("department")]
    public string Department { get; set; } = string.Empty;

    [BsonElement("plan_code")]
    public string PlanCode { get; set; } = string.Empty;

    [BsonElement("plan_name")]
    public string PlanName { get; set; } = string.Empty;

    [BsonElement("trigger")]
    public string Trigger { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("scope")]
    public string Scope { get; set; } = string.Empty;

    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;

    [BsonElement("reference_standard")]
    public string ReferenceStandard { get; set; } = string.Empty;

    [BsonElement("permit_required")]
    public bool PermitRequired { get; set; }

    [BsonElement("evidence_required")]
    public bool EvidenceRequired { get; set; }

    [BsonElement("asset_code")]
    public string AssetCode { get; set; } = string.Empty;

    [BsonElement("asset_name")]
    public string AssetName { get; set; } = string.Empty;

    [BsonElement("location")]
    public string Location { get; set; } = string.Empty;

    [BsonElement("assigned_to")]
    public string AssignedTo { get; set; } = string.Empty;

    [BsonElement("assigned_team")]
    public string AssignedTeam { get; set; } = string.Empty;

    [BsonElement("due_date")]
    public string? DueDate { get; set; }

    [BsonElement("scheduled_start")]
    public string? ScheduledStart { get; set; }

    [BsonElement("scheduled_end")]
    public string? ScheduledEnd { get; set; }

    [BsonElement("started_at")]
    public string? StartedAt { get; set; }

    [BsonElement("completed_at")]
    public string? CompletedAt { get; set; }

    [BsonElement("findings_count")]
    public int FindingsCount { get; set; }

    [BsonElement("checklist_code")]
    public string ChecklistCode { get; set; } = string.Empty;

    [BsonElement("checklist_name")]
    public string ChecklistName { get; set; } = string.Empty;

    [BsonElement("responses")]
    public List<InspectionChecklistResponse> Responses { get; set; } = [];

    [BsonElement("sign_off")]
    public List<SignOffStep> SignOff { get; set; } = [];
}
