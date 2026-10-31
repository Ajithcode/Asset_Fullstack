using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Inspection.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class FindingActivity
{
    [BsonElement("at")]
    public string At { get; set; } = string.Empty;

    [BsonElement("by")]
    public string By { get; set; } = string.Empty;

    [BsonElement("text")]
    public string Text { get; set; } = string.Empty;
}

[BsonIgnoreExtraElements]
public sealed class InspectionFinding : BaseEntity
{
    [BsonElement("finding_code")]
    public string FindingCode { get; set; } = string.Empty;

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("inspection_code")]
    public string InspectionCode { get; set; } = string.Empty;

    [BsonElement("asset_code")]
    public string AssetCode { get; set; } = string.Empty;

    [BsonElement("asset_name")]
    public string AssetName { get; set; } = string.Empty;

    [BsonElement("source_task")]
    public string SourceTask { get; set; } = string.Empty;

    [BsonElement("severity")]
    public string Severity { get; set; } = string.Empty;

    [BsonElement("owner")]
    public string Owner { get; set; } = string.Empty;

    [BsonElement("due_date")]
    public string? DueDate { get; set; }

    [BsonElement("recorded_value")]
    public string RecordedValue { get; set; } = string.Empty;

    [BsonElement("acceptance")]
    public string Acceptance { get; set; } = string.Empty;

    [BsonElement("observation")]
    public string Observation { get; set; } = string.Empty;

    [BsonElement("evidence")]
    public List<string> Evidence { get; set; } = [];

    [BsonElement("corrective_action")]
    public string CorrectiveAction { get; set; } = string.Empty;

    [BsonElement("assigned_to")]
    public string AssignedTo { get; set; } = string.Empty;

    [BsonElement("work_order")]
    public string WorkOrder { get; set; } = string.Empty;

    [BsonElement("work_order_status")]
    public string WorkOrderStatus { get; set; } = string.Empty;

    [BsonElement("workflow_step")]
    public string WorkflowStep { get; set; } = string.Empty;

    [BsonElement("activity")]
    public List<FindingActivity> Activity { get; set; } = [];
}
