using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Inspection.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class InspectionTask : BaseEntity
{
    [BsonElement("task_code")]
    public string TaskCode { get; set; } = string.Empty;

    [BsonElement("asset_id")]
    public string AssetId { get; set; } = string.Empty;

    [BsonElement("asset_name")]
    public string AssetName { get; set; } = string.Empty;

    [BsonElement("task_title")]
    public string TaskTitle { get; set; } = string.Empty;

    [BsonElement("task_category")]
    public string TaskCategory { get; set; } = string.Empty;

    [BsonElement("task_description")]
    public string TaskDescription { get; set; } = string.Empty;

    [BsonElement("response_type")]
    public string ResponseType { get; set; } = string.Empty;

    [BsonElement("is_critical_task")]
    public bool IsCriticalTask { get; set; }

    [BsonElement("is_mandatory")]
    public bool IsMandatory { get; set; }

    [BsonElement("is_active")]
    public bool IsActive { get; set; } = true;
}
