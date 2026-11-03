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

    // Built-in icon key, used when no image is uploaded.
    [BsonElement("icon")]
    public string? Icon { get; set; }

    // Uploaded picture as a data URL.
    [BsonElement("image")]
    public string? Image { get; set; }

    // Where this level sits on the risk matrix.
    [BsonElement("likelihood")]
    public string? Likelihood { get; set; }

    [BsonElement("impact")]
    public string? Impact { get; set; }

    [BsonElement("business_impact")]
    public CriticalityBusinessImpact BusinessImpact { get; set; } = new();

    [BsonElement("recommended_strategy")]
    public string? RecommendedStrategy { get; set; }

    [BsonElement("inspection_frequency")]
    public string? InspectionFrequency { get; set; }

    [BsonElement("condition_monitoring")]
    public string? ConditionMonitoring { get; set; }

    [BsonElement("spare_parts_priority")]
    public string? SparePartsPriority { get; set; }

    [BsonElement("response_time_hours")]
    public int? ResponseTimeHours { get; set; }

    [BsonElement("escalate_on_failure")]
    public bool EscalateOnFailure { get; set; }

    [BsonElement("root_cause_required")]
    public bool RootCauseRequired { get; set; }

    [BsonElement("approval_for_disposal")]
    public bool ApprovalForDisposal { get; set; }

    [BsonElement("show_in_reports")]
    public bool ShowInReports { get; set; } = true;
}

// What a failure of an asset at this level means for each area, as free text.
public class CriticalityBusinessImpact
{
    [BsonElement("safety")]
    public string? Safety { get; set; }

    [BsonElement("operational")]
    public string? Operational { get; set; }

    [BsonElement("financial")]
    public string? Financial { get; set; }

    [BsonElement("regulatory")]
    public string? Regulatory { get; set; }

    [BsonElement("reputation")]
    public string? Reputation { get; set; }

    [BsonElement("environmental")]
    public string? Environmental { get; set; }
}
