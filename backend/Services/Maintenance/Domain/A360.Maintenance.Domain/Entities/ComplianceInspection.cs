
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Maintenance.Domain.Entities;

public class ComplianceInspection : BaseEntity
{
    [BsonElement("inspection_id")]
    public string InspectionId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string AssetId { get; set; } = null!;

    [BsonElement("asset_name")]
    public string AssetName { get; set; } = null!;

    [BsonElement("inspection_type")]
    public string InspectionType { get; set; } = null!;

    [BsonElement("checklist")]
    public List<string>? Checklist { get; set; }

    [BsonElement("inspector_name")]
    public string? InspectorName { get; set; }

    [BsonElement("result")]
    public string? Result { get; set; }

    [BsonElement("next_inspection_date")]
    public DateTime? NextInspectionDate { get; set; }
}
