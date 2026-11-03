
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Maintenance.Domain.Entities;

public class PredictiveMaintenance : BaseEntity
{
    [BsonElement("asset_id")]
    public string AssetId { get; set; } = null!;

    [BsonElement("asset_name")]
    public string AssetName { get; set; } = null!;

    [BsonElement("sensor_type")]
    public string SensorType { get; set; } = null!;

    [BsonElement("threshold_value")]
    public double ThresholdValue { get; set; }

    [BsonElement("alert_condition")]
    public string? AlertCondition { get; set; }

    [BsonElement("data_source_device_id")]
    public string? DataSourceDeviceId { get; set; }

    [BsonElement("prediction_model_output")]
    public string? PredictionModelOutput { get; set; }

    [BsonElement("risk_level")]
    public string RiskLevel { get; set; } = null!;
}
