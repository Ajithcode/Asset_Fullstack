using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Tracking.Domain.Entities;

/// <summary>
/// IoT Events Timeline → Telemetry Data: one value reported by a device or sensor
/// (e.g. TR000001, VS-30018 / SEN-V-30018, Vibration 4.2 mm/s at 10:15).
/// </summary>
/// <remarks>Readings are a log; the sensor record keeps only the latest value as its live reading.</remarks>
[BsonIgnoreExtraElements]
public sealed class TelemetryReading : BaseEntity
{
    [BsonElement("reading_id")]
    public string ReadingId { get; set; } = string.Empty;

    [BsonElement("recorded_at")]
    public DateTime RecordedAt { get; set; }

    [BsonElement("device_id")]
    public string DeviceId { get; set; } = string.Empty;

    [BsonElement("sensor_id")]
    public string? SensorId { get; set; }

    [BsonElement("asset_registration_id")]
    public string? AssetRegistrationId { get; set; }

    /// <summary>e.g. Temperature, Vibration, Battery.</summary>
    [BsonElement("parameter")]
    public string Parameter { get; set; } = string.Empty;

    [BsonElement("value")]
    public decimal Value { get; set; }

    [BsonElement("unit")]
    public string? Unit { get; set; }
}
