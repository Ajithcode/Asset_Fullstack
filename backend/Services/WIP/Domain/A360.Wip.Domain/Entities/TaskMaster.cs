
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Wip.Domain.Entities;

public class TaskMaster : BaseEntity
{
    [BsonElement("task_id")]
    public string TaskId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string? AssetId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("job_id")]
    public string? JobId { get; set; }

    [BsonElement("task_name")]
    public string TaskName { get; set; } = null!;

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("sequence_order")]
    public int? SequenceOrder { get; set; }

    [BsonElement("assigned_to")]
    public string? AssignedTo { get; set; }

    [BsonElement("planned_start_time")]
    public DateTime? PlannedStartTime { get; set; }

    [BsonElement("planned_end_time")]
    public DateTime? PlannedEndTime { get; set; }

    [BsonElement("actual_start_time")]
    public DateTime? ActualStartTime { get; set; }

    [BsonElement("actual_end_time")]
    public DateTime? ActualEndTime { get; set; }

    [BsonElement("dependency_task_id")]
    public string? DependencyTaskId { get; set; }

    [BsonElement("checklist_id")]
    public string? ChecklistId { get; set; }

    [BsonElement("completion_percentage")]
    public double? CompletionPercentage { get; set; }

    [BsonElement("remarks")]
    public string? Remarks { get; set; }
}
