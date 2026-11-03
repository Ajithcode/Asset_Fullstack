using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Maintenance.Domain.Entities;

/// <summary>Condition-monitoring rule and latest model prediction for one asset.</summary>
[BsonIgnoreExtraElements]
public class PredictiveMaintenance : BaseEntity
{
    [BsonElement("asset_id")]
    public string AssetId { get; set; } = null!;

    [BsonElement("asset_name")]
    public string AssetName { get; set; } = null!;

    [BsonElement("asset_type")]
    public string? AssetType { get; set; }

    [BsonElement("location")]
    public string? Location { get; set; }

    [BsonElement("sensor_type")]
    public string SensorType { get; set; } = null!;

    [BsonElement("threshold_value")]
    public double ThresholdValue { get; set; }

    [BsonElement("alert_condition")]
    public string? AlertCondition { get; set; }

    [BsonElement("data_source_device_id")]
    public string? DataSourceDeviceId { get; set; }

    [BsonElement("last_reading")]
    public double? LastReading { get; set; }

    [BsonElement("last_reading_at")]
    public DateTime? LastReadingAt { get; set; }

    [BsonElement("prediction_model_output")]
    public string? PredictionModelOutput { get; set; }

    [BsonElement("model_name")]
    public string? ModelName { get; set; }

    /// <summary>Healthy, At Risk, High.</summary>
    [BsonElement("risk_level")]
    public string RiskLevel { get; set; } = null!;

    /// <summary>0-100, higher is healthier.</summary>
    [BsonElement("health_score")]
    public double? HealthScore { get; set; }

    [BsonElement("predicted_issue")]
    public string? PredictedIssue { get; set; }

    [BsonElement("predicted_failure_date")]
    public DateTime? PredictedFailureDate { get; set; }

    /// <summary>Model confidence, 0-100 %.</summary>
    [BsonElement("confidence")]
    public double? Confidence { get; set; }

    [BsonElement("recommended_action")]
    public string? RecommendedAction { get; set; }

    /// <summary>Set when the predicted failure actually happened; used for model accuracy.</summary>
    [BsonElement("actual_failure_date")]
    public DateTime? ActualFailureDate { get; set; }

    [BsonElement("work_order_id")]
    public string? WorkOrderId { get; set; }

    [BsonElement("anomalies")]
    public List<PredictiveAnomaly> Anomalies { get; set; } = [];
}

[BsonIgnoreExtraElements]
public sealed class PredictiveAnomaly
{
    [BsonElement("detected_at")]
    public DateTime DetectedAt { get; set; }

    [BsonElement("metric")]
    public string Metric { get; set; } = string.Empty;

    [BsonElement("value")]
    public double Value { get; set; }

    [BsonElement("expected")]
    public string? Expected { get; set; }

    [BsonElement("severity")]
    public string? Severity { get; set; }

    [BsonElement("note")]
    public string? Note { get; set; }
}
