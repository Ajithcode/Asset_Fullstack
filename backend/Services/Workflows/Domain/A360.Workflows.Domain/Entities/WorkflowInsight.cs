
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Workflows.Domain.Entities;

public class WorkflowInsight : BaseEntity
{
    [BsonElement("workflow_name")]
    public string WorkflowName { get; set; } = null!;

    [BsonElement("module")]
    public string? Module { get; set; }

    [BsonElement("pending_approvals")]
    public int PendingApprovals { get; set; }

    [BsonElement("total_instances")]
    public int TotalInstances { get; set; }

    [BsonElement("avg_approval_time_hrs")]
    public double AvgApprovalTimeHrs { get; set; }
}
