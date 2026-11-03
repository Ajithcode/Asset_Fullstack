using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Inspection.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class ChecklistTaskItem
{
    [BsonElement("task_id")]
    public string TaskId { get; set; } = string.Empty;

    [BsonElement("task_title")]
    public string TaskTitle { get; set; } = string.Empty;

    [BsonElement("is_mandatory")]
    public bool IsMandatory { get; set; }

    [BsonElement("order")]
    public int Order { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class ChecklistSection
{
    [BsonElement("section_name")]
    public string SectionName { get; set; } = string.Empty;

    [BsonElement("order")]
    public int Order { get; set; }

    [BsonElement("tasks")]
    public List<ChecklistTaskItem> Tasks { get; set; } = [];
}

[BsonIgnoreExtraElements]
public sealed class Checklist : BaseEntity
{
    [BsonElement("checklist_code")]
    public string ChecklistCode { get; set; } = string.Empty;

    [BsonElement("template_name")]
    public string TemplateName { get; set; } = string.Empty;

    [BsonElement("inspection_type_id")]
    public string InspectionTypeId { get; set; } = string.Empty;

    [BsonElement("inspection_type_name")]
    public string InspectionTypeName { get; set; } = string.Empty;

    [BsonElement("asset_category")]
    public string AssetCategory { get; set; } = string.Empty;

    [BsonElement("asset_type")]
    public string AssetType { get; set; } = string.Empty;

    [BsonElement("sections")]
    public List<ChecklistSection> Sections { get; set; } = [];

    [BsonElement("version")]
    public int Version { get; set; } = 1;

    [BsonElement("effective_date")]
    public DateTime? EffectiveDate { get; set; }

    [BsonElement("is_active")]
    public bool IsActive { get; set; } = true;
}
