using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Inspection.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class InspectionPlan : BaseEntity
{
    [BsonElement("plan_code")]
    public string PlanCode { get; set; } = string.Empty;

    [BsonElement("plan_name")]
    public string PlanName { get; set; } = string.Empty;

    [BsonElement("inspection_type_name")]
    public string InspectionTypeName { get; set; } = string.Empty;

    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;

    [BsonElement("frequency")]
    public string Frequency { get; set; } = string.Empty;

    [BsonElement("asset_count")]
    public int AssetCount { get; set; }

    [BsonElement("next_due_date")]
    public string? NextDueDate { get; set; }

    [BsonElement("inspector_name")]
    public string InspectorName { get; set; } = string.Empty;

    [BsonElement("checklist_code")]
    public string ChecklistCode { get; set; } = string.Empty;

    [BsonElement("checklist_name")]
    public string ChecklistName { get; set; } = string.Empty;

    [BsonElement("lead_time_days")]
    public int LeadTimeDays { get; set; }

    [BsonElement("auto_generate")]
    public bool AutoGenerate { get; set; }

    [BsonElement("is_active")]
    public bool IsActive { get; set; } = true;
}
