
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Workflows.Domain.Entities;

public class WorkflowInstance : BaseEntity
{
    [BsonElement("workflow_name")]
    public string WorkflowName { get; set; } = null!;

    [BsonElement("related_asset_request")]
    public string? RelatedAssetRequest { get; set; }

    [BsonElement("requested_by")]
    public string? RequestedBy { get; set; }

    [BsonElement("current_step")]
    public string? CurrentStep { get; set; }

    [BsonElement("started_on")]
    public DateTime? StartedOn { get; set; }
}
