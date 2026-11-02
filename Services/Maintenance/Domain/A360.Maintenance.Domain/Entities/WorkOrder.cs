using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Maintenance.Domain.Entities;

[BsonIgnoreExtraElements]
public class WorkOrder : BaseEntity
{
    [BsonElement("work_order_id")]
    public string WorkOrderId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string AssetId { get; set; } = null!;

    [BsonElement("asset_name")]
    public string AssetName { get; set; } = null!;

    /// <summary>Maintenance type: Preventive, Corrective, Predictive, Inspection, Service.</summary>
    [BsonElement("work_type")]
    public string WorkType { get; set; } = null!;

    [BsonElement("title")]
    public string Title { get; set; } = null!;

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("priority")]
    public string Priority { get; set; } = null!;

    [BsonElement("scheduled_date")]
    public DateTime? ScheduledDate { get; set; }

    [BsonElement("assigned_technician")]
    public string? AssignedTechnician { get; set; }

    /// <summary>Routine, Breakdown, Emergency, Improvement, Statutory.</summary>
    [BsonElement("category")]
    public string? Category { get; set; }

    [BsonElement("due_date")]
    public DateTime? DueDate { get; set; }

    [BsonElement("planned_start_date")]
    public DateTime? PlannedStartDate { get; set; }

    [BsonElement("planned_end_date")]
    public DateTime? PlannedEndDate { get; set; }

    [BsonElement("actual_start_date")]
    public DateTime? ActualStartDate { get; set; }

    [BsonElement("actual_end_date")]
    public DateTime? ActualEndDate { get; set; }

    [BsonElement("estimated_hours")]
    public double? EstimatedHours { get; set; }

    /// <summary>Where the work order came from: Manual, PM Schedule, Service Request, Predictive Alert, Inspection.</summary>
    [BsonElement("source")]
    public string? Source { get; set; }

    /// <summary>Code of the originating record (PM schedule, service request, predictive rule).</summary>
    [BsonElement("source_reference")]
    public string? SourceReference { get; set; }

    [BsonElement("site")]
    public string? Site { get; set; }

    [BsonElement("building")]
    public string? Building { get; set; }

    [BsonElement("area")]
    public string? Area { get; set; }

    [BsonElement("department")]
    public string? Department { get; set; }

    [BsonElement("team")]
    public string? Team { get; set; }

    [BsonElement("vendor")]
    public string? Vendor { get; set; }

    [BsonElement("notes")]
    public string? Notes { get; set; }

    /// <summary>0-100. Derived from the task list when tasks exist; 100 when Completed.</summary>
    [BsonElement("progress")]
    public int Progress { get; set; }

    [BsonElement("tasks")]
    public List<WorkOrderTask> Tasks { get; set; } = [];

    [BsonElement("parts")]
    public List<WorkOrderPart> Parts { get; set; } = [];

    [BsonElement("labour")]
    public List<WorkOrderLabour> Labour { get; set; } = [];

    /// <summary>Costs outside parts and labour (contractor, rental, freight).</summary>
    [BsonElement("additional_cost")]
    public double AdditionalCost { get; set; }

    [BsonElement("documents")]
    public List<WorkOrderDocument> Documents { get; set; } = [];

    /// <summary>Server-maintained trail of changes.</summary>
    [BsonElement("history")]
    public List<WorkOrderHistoryEntry> History { get; set; } = [];
}

[BsonIgnoreExtraElements]
public sealed class WorkOrderTask
{
    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("assigned_to")]
    public string? AssignedTo { get; set; }

    [BsonElement("is_done")]
    public bool IsDone { get; set; }

    [BsonElement("completed_at")]
    public DateTime? CompletedAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class WorkOrderPart
{
    [BsonElement("part_number")]
    public string? PartNumber { get; set; }

    [BsonElement("part_name")]
    public string PartName { get; set; } = string.Empty;

    [BsonElement("quantity")]
    public double Quantity { get; set; }

    [BsonElement("unit_cost")]
    public double UnitCost { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class WorkOrderLabour
{
    [BsonElement("technician")]
    public string Technician { get; set; } = string.Empty;

    [BsonElement("role")]
    public string? Role { get; set; }

    [BsonElement("hours")]
    public double Hours { get; set; }

    [BsonElement("hourly_rate")]
    public double HourlyRate { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class WorkOrderDocument
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("url")]
    public string? Url { get; set; }

    [BsonElement("file_type")]
    public string? FileType { get; set; }

    [BsonElement("uploaded_by")]
    public string? UploadedBy { get; set; }

    [BsonElement("uploaded_at")]
    public DateTime? UploadedAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class WorkOrderHistoryEntry
{
    [BsonElement("at")]
    public DateTime At { get; set; }

    [BsonElement("by")]
    public string? By { get; set; }

    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;

    [BsonElement("details")]
    public string? Details { get; set; }
}
