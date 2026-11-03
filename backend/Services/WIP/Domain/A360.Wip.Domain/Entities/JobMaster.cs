
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Wip.Domain.Entities;

public class JobMaster : BaseEntity
{
    [BsonElement("job_id")]
    public string JobId { get; set; } = null!;

    [BsonElement("job_name")]
    public string JobName { get; set; } = null!;

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("asset_id")]
    public string AssetId { get; set; } = null!;

    [BsonElement("asset_category")]
    public string? AssetCategory { get; set; }

    [BsonElement("location_id")]
    public string? LocationId { get; set; }

    [BsonElement("department_id")]
    public string? DepartmentId { get; set; }

    [BsonElement("work_type")]
    public string? WorkType { get; set; }

    [BsonElement("priority")]
    public string? Priority { get; set; }

    [BsonElement("planned_start_date")]
    public DateTime? PlannedStartDate { get; set; }

    [BsonElement("planned_end_date")]
    public DateTime? PlannedEndDate { get; set; }

    [BsonElement("assigned_to")]
    public string? AssignedTo { get; set; }

    [BsonElement("supervisor_id")]
    public string? SupervisorId { get; set; }

    [BsonElement("progress_percentage")]
    public double? ProgressPercentage { get; set; }
}
