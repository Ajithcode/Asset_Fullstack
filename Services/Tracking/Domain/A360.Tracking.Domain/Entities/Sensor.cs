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

    /// <summary>The number part of the sensor id, e.g. 80900 in SEN-T-80900.</summary>
    [BsonElement("sensor_number")]
    public string? SensorNumber { get; set; }

    /// <summary>Manufacturer serial number, e.g. TS300-80214-7765. Unique, ignoring case.</summary>
    [BsonElement("serial_number")]
    public string? SerialNumber { get; set; }

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

    /// <summary>Channel of the linked device this sensor reads from; a device can expose several channels.</summary>
    [BsonElement("channel")]
    public int? Channel { get; set; }

    /// <summary>One of <see cref="TelemetrySamplingInterval.Seconds"/>.</summary>
    [BsonElement("sampling_interval_seconds")]
    public int? SamplingIntervalSeconds { get; set; }

    /// <summary>Reading below this (in <see cref="Unit"/>) raises a low alarm.</summary>
    [BsonElement("low_alarm")]
    public decimal? LowAlarm { get; set; }

    /// <summary>Reading above this (in <see cref="Unit"/>) raises a high alarm.</summary>
    [BsonElement("high_alarm")]
    public decimal? HighAlarm { get; set; }

    /// <summary>Master-data site id (MasterDataManagement /api/master-data/sites).</summary>
    [BsonElement("site_id")]
    public string? SiteId { get; set; }

    [BsonElement("site_name")]
    public string? SiteName { get; set; }

    /// <summary>Code of the linked asset, e.g. CR-03.</summary>
    [BsonElement("asset_code")]
    public string? AssetCode { get; set; }

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

/// <summary>A Sensor type option, with what it measures and the prefix of its sensor ids (e.g. SEN-T + 80900 → SEN-T-80900).</summary>
public sealed record SensorTypeOption(string SensorType, string IdPrefix, string MeasuredParameter, string Unit)
{
    public static readonly IReadOnlyList<SensorTypeOption> All =
    [
        new("Temperature Probe", "SEN-T", "Temperature", "°C"),
        new("Humidity Sensor", "SEN-H", "Relative Humidity", "%RH"),
        new("Vibration Sensor", "SEN-V", "Vibration", "mm/s"),
        new("Pressure Transmitter", "SEN-P", "Pressure", "bar"),
        new("Current Sensor", "SEN-C", "Current", "A")
    ];

    public static SensorTypeOption? Find(string? sensorType)
    {
        return All.FirstOrDefault(option => string.Equals(option.SensorType, sensorType?.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Common measurement points per sensor type, suggested in the Measurement point field.</summary>
public static class SensorMeasurementPoint
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> BySensorType =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Temperature Probe"] = ["Supply Air", "Return Air", "Ambient", "Product Core", "Evaporator", "Condenser", "Bearing", "Winding"],
            ["Humidity Sensor"] = ["Supply Air", "Return Air", "Ambient", "Storage Area"],
            ["Vibration Sensor"] = ["Motor DE", "Motor NDE", "Pump DE", "Pump NDE", "Gearbox", "Fan Bearing"],
            ["Pressure Transmitter"] = ["Suction", "Discharge", "Inlet", "Outlet", "Header", "Filter Differential"],
            ["Current Sensor"] = ["Output L1", "Output L2", "Output L3", "Neutral", "Motor Feed"],
        };
}

public static class SensorStatus
{
    public const string Active = "Active";
    public const string Offline = "Offline";
    public const string Attention = "Attention";
    public const string Inactive = "Inactive";

    public static readonly IReadOnlyList<string> All = [Active, Offline, Attention, Inactive];
}
