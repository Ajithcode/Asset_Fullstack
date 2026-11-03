using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>An action to lower the criticality of one registered asset, for the Risk Mitigation tab.</summary>
/// <remarks>Kept per asset, so actions carry over when the asset is re-assessed. Status is kept in BaseEntity.Status.</remarks>
[BsonIgnoreExtraElements]
public sealed class AssetRiskMitigationAction : BaseEntity
{
    [BsonElement("asset_registration_id")]
    public string AssetRegistrationId { get; set; } = string.Empty;

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("asset_tag")]
    public string? AssetTag { get; set; }

    // e.g. "Monthly no-load run and quarterly load bank test".
    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;

    // Person or team, e.g. "Maintenance Team".
    [BsonElement("owner")]
    public string Owner { get; set; } = string.Empty;

    [BsonElement("due_date")]
    public DateTime DueDate { get; set; }

    // The criticality level expected once the action is done: "Low", "Medium", "High" or "Critical".
    [BsonElement("expected_criticality")]
    public string? ExpectedCriticality { get; set; }

    // When Status last changed to Completed.
    [BsonElement("completed_at")]
    public DateTime? CompletedAt { get; set; }
}
