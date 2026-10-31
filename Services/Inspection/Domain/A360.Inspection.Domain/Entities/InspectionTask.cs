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

    [BsonElement("unit")]
    public string Unit { get; set; } = string.Empty;

    [BsonElement("estimated_duration_minutes")]
    public int EstimatedDurationMinutes { get; set; }

    [BsonElement("min_value")]
    public double? MinValue { get; set; }

    [BsonElement("max_value")]
    public double? MaxValue { get; set; }

    [BsonElement("procedure")]
    public string Procedure { get; set; } = string.Empty;

    [BsonElement("reading_required")]
    public bool ReadingRequired { get; set; }

    [BsonElement("photo_required_on_failure")]
    public bool PhotoRequiredOnFailure { get; set; }

    [BsonElement("create_finding_outside_limits")]
    public bool CreateFindingOutsideLimits { get; set; }

    [BsonElement("default_severity")]
    public string DefaultSeverity { get; set; } = string.Empty;
}
