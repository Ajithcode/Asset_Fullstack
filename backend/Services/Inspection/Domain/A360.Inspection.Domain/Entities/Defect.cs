using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Inspection.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class Defect : BaseEntity
{
    [BsonElement("defect_code")]
    public string DefectCode { get; set; } = string.Empty;

    [BsonElement("defect_name")]
    public string DefectName { get; set; } = string.Empty;

    [BsonElement("defect_category")]
    public string DefectCategory { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("severity")]
    public string Severity { get; set; } = string.Empty;

    [BsonElement("risk_rating")]
    public string RiskRating { get; set; } = string.Empty;

    [BsonElement("recommended_corrective_action")]
    public string RecommendedCorrectiveAction { get; set; } = string.Empty;

    [BsonElement("default_resolution_period")]
    public string DefaultResolutionPeriod { get; set; } = string.Empty;

    [BsonElement("is_active")]
    public bool IsActive { get; set; } = true;
}
