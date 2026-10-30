
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Wip.Domain.Entities;

public class IssueMaster : BaseEntity
{
    [BsonElement("issue_id")]
    public string IssueId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string? AssetId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("job_id")]
    public string? JobId { get; set; }

    [BsonElement("task_id")]
    public string? TaskId { get; set; }

    [BsonElement("issue_type")]
    public string? IssueType { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("reported_by")]
    public string? ReportedBy { get; set; }

    [BsonElement("reported_date")]
    public DateTime? ReportedDate { get; set; }

    [BsonElement("severity")]
    public string? Severity { get; set; }

    [BsonElement("resolution_remarks")]
    public string? ResolutionRemarks { get; set; }

    [BsonElement("closed_date")]
    public DateTime? ClosedDate { get; set; }
}
