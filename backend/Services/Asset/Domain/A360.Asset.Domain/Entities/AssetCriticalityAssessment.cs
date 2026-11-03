using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>A criticality assessment of one registered asset, for the Criticality tab and the Asset Criticality List.</summary>
/// <remarks>
/// Each assess / re-assess saves a new record; IsCurrent marks the latest one for the asset, and the older ones
/// are its Review History. Score and level are worked out from the criteria when saved.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class AssetCriticalityAssessment : BaseEntity
{
    [BsonElement("asset_registration_id")]
    public string AssetRegistrationId { get; set; } = string.Empty;

    // Copied from the asset when the assessment is saved, so the list can be searched and filtered without loading assets.
    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("asset_tag")]
    public string? AssetTag { get; set; }

    [BsonElement("category")]
    public string? Category { get; set; }

    [BsonElement("location")]
    public string? Location { get; set; }

    [BsonElement("asset_status")]
    public string? AssetStatus { get; set; }

    [BsonElement("is_current")]
    public bool IsCurrent { get; set; }

    [BsonElement("assessment_date")]
    public DateTime AssessmentDate { get; set; }

    [BsonElement("valid_until")]
    public DateTime? ValidUntil { get; set; }

    [BsonElement("next_review_date")]
    public DateTime? NextReviewDate { get; set; }

    [BsonElement("assessed_by")]
    public string AssessedBy { get; set; } = string.Empty;

    [BsonElement("approved_by")]
    public string ApprovedBy { get; set; } = string.Empty;

    // "Very Low", "Low", "Medium", "High" or "Very High"; the Criticality Matrix position.
    [BsonElement("likelihood")]
    public string? Likelihood { get; set; }

    [BsonElement("consequence")]
    public string? Consequence { get; set; }

    [BsonElement("criteria")]
    public List<AssetCriticalityCriterion> Criteria { get; set; } = [];

    // Sum of the weighted criteria, 0 to 100.
    [BsonElement("criticality_score")]
    public decimal CriticalityScore { get; set; }

    // "Low", "Medium", "High" or "Critical", from the score.
    [BsonElement("criticality_level")]
    public string CriticalityLevel { get; set; } = string.Empty;

    [BsonElement("remarks")]
    public string? Remarks { get; set; }
}

/// <summary>One row of the criteria table, e.g. Operational Impact, weight 25, score 5, weighted 25.0.</summary>
public sealed class AssetCriticalityCriterion
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    // Percent; the weights of one assessment add up to 100.
    [BsonElement("weight")]
    public decimal Weight { get; set; }

    // 1 to 5 in steps of 0.5.
    [BsonElement("score")]
    public decimal Score { get; set; }

    // Weight x Score / 5.
    [BsonElement("weighted_score")]
    public decimal WeightedScore { get; set; }

    [BsonElement("justification")]
    public string Justification { get; set; } = string.Empty;
}
