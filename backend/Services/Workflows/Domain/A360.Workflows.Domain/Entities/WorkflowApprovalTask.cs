
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Workflows.Domain.Entities;

public class WorkflowApprovalTask : BaseEntity
{
    [BsonElement("workflow_name")]
    public string WorkflowName { get; set; } = null!;

    [BsonElement("asset_request")]
    public string? AssetRequest { get; set; }

    [BsonElement("requested_by")]
    public string? RequestedBy { get; set; }

    [BsonElement("assigned_to")]
    public string? AssignedTo { get; set; }

    [BsonElement("due_date")]
    public DateTime? DueDate { get; set; }

    [BsonElement("priority")]
    public string? Priority { get; set; }
}
