using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Tracking.Domain.Entities;

/// <summary>Sensor Management: one measurement point on a device (e.g. SEN-T-80214, a temperature probe on TS-80214 measuring Cold Room CR-03 supply air).</summary>
/// <remarks>
/// A device can expose several sensor channels; each is its own record. One record per sensor id.
/// Status is kept in BaseEntity.Status and is one of <see cref="SensorStatus"/>.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class Sensor : BaseEntity
{
    [BsonElement("sensor_id")]
    public string SensorId { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>e.g. Temperature Probe, Vibration Sensor, Pressure Transmitter.</summary>
    [BsonElement("sensor_type")]
    public string SensorType { get; set; } = string.Empty;

    /// <summary>e.g. Temperature, Relative Humidity.</summary>
    [BsonElement("measured_parameter")]
    public string MeasuredParameter { get; set; } = string.Empty;

    /// <summary>Unit of the parameter and of the live reading, e.g. °C, %RH, bar.</summary>
    [BsonElement("unit")]
    public string? Unit { get; set; }

    [BsonElement("device_id")]
    public string? DeviceId { get; set; }

    [BsonElement("device_model")]
    public string? DeviceModel { get; set; }

    [BsonElement("asset_registration_id")]
    public string? AssetRegistrationId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    /// <summary>Where on the asset the sensor measures, e.g. Supply Air, Motor DE.</summary>
    [BsonElement("measurement_point")]
    public string? MeasurementPoint { get; set; }

    /// <summary>Latest value in <see cref="Unit"/>; null when there is no data.</summary>
    [BsonElement("live_reading")]
    public decimal? LiveReading { get; set; }

    [BsonElement("calibration_due_date")]
    public DateTime? CalibrationDueDate { get; set; }
}

public static class SensorStatus
{
    public const string Active = "Active";
    public const string Offline = "Offline";
    public const string Attention = "Attention";
    public const string Inactive = "Inactive";

    public static readonly IReadOnlyList<string> All = [Active, Offline, Attention, Inactive];
}
