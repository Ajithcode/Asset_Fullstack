
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Workflows.Domain.Entities;

public class WorkflowBuilder : BaseEntity
{
    [BsonElement("workflow_name")]
    public string WorkflowName { get; set; } = null!;

    [BsonElement("module")]
    public string? Module { get; set; }

    [BsonElement("trigger_event")]
    public string? TriggerEvent { get; set; }

    [BsonElement("version")]
    public string? Version { get; set; }

    [BsonElement("start_from_template")]
    public bool StartFromTemplate { get; set; }
}
