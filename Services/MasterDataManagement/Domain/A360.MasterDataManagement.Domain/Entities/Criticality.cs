using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

public class Criticality : BaseEntity
{
    [BsonElement("criticality_name")]
    public string CriticalityName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Hex colour used to show the criticality, e.g. "#C62828".
    [BsonElement("color")]
    public string? Color { get; set; }

    [BsonElement("risk_level")]
    public string? RiskLevel { get; set; }

    [BsonElement("maintenance_priority")]
    public string? MaintenancePriority { get; set; }

    // At most one criticality per client is the default.
    [BsonElement("default_criticality")]
    public bool DefaultCriticality { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // Asset Criticality score range of this level (0-100, whole numbers); null on levels that are not score based.
    [BsonElement("min_score")]
    public int? MinScore { get; set; }

    [BsonElement("max_score")]
    public int? MaxScore { get; set; }

    // Display order, 1 = most severe (Critical).
    [BsonElement("display_order")]
    public int? DisplayOrder { get; set; }
}
