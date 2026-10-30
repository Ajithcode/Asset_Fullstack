
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Maintenance.Domain.Entities;

public class MaintenanceTask : BaseEntity
{
    [BsonElement("asset_id")]
    public string AssetId { get; set; } = null!;

    [BsonElement("asset_name")]
    public string AssetName { get; set; } = null!;

    [BsonElement("task_checklist")]
    public List<string>? TaskChecklist { get; set; }

    [BsonElement("instructions")]
    public string? Instructions { get; set; }

    [BsonElement("tools_required")]
    public List<string>? ToolsRequired { get; set; }

    [BsonElement("safety_procedures")]
    public List<string>? SafetyProcedures { get; set; }

    [BsonElement("estimated_duration")]
    public string? EstimatedDuration { get; set; }
}
