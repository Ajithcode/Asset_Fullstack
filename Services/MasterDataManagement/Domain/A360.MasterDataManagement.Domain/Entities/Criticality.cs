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
}
