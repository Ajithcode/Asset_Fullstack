using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>A condition assessment recorded against one registered asset, for the Asset Status &amp; Condition screen.</summary>
/// <remarks>The latest assessment by date is the asset's current condition.</remarks>
[BsonIgnoreExtraElements]
public sealed class AssetConditionAssessment : BaseEntity
{
    [BsonElement("asset_registration_id")]
    public string AssetRegistrationId { get; set; } = string.Empty;

    // Copied from the asset when the assessment is saved, so the list can be shown without loading assets.
    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("asset_tag")]
    public string? AssetTag { get; set; }

    [BsonElement("assessment_date")]
    public DateTime AssessmentDate { get; set; }

    // Person or team, e.g. "HSE Team".
    [BsonElement("assessed_by")]
    public string AssessedBy { get; set; } = string.Empty;

    // "Excellent", "Good", "Fair", "Poor" or "Critical".
    [BsonElement("overall_condition")]
    public string OverallCondition { get; set; } = string.Empty;

    // 0 to 100.
    [BsonElement("health_score")]
    public decimal HealthScore { get; set; }

    [BsonElement("physical_condition")]
    public string? PhysicalCondition { get; set; }

    [BsonElement("performance")]
    public string? Performance { get; set; }

    [BsonElement("safety")]
    public string? Safety { get; set; }

    [BsonElement("remarks")]
    public string? Remarks { get; set; }
}
