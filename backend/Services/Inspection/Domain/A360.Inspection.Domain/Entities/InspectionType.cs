using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Inspection.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class InspectionType : BaseEntity
{
    [BsonElement("inspection_type_code")]
    public string InspectionTypeCode { get; set; } = string.Empty;

    [BsonElement("asset_id")]
    public string AssetId { get; set; } = string.Empty;

    [BsonElement("asset_name")]
    public string AssetName { get; set; } = string.Empty;

    [BsonElement("inspection_type_name")]
    public string InspectionTypeName { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("default_priority")]
    public string DefaultPriority { get; set; } = string.Empty;

    [BsonElement("default_approval_workflow")]
    public string DefaultApprovalWorkflow { get; set; } = string.Empty;

    [BsonElement("default_report_template")]
    public string DefaultReportTemplate { get; set; } = string.Empty;

    [BsonElement("is_active")]
    public bool IsActive { get; set; } = true;
}
