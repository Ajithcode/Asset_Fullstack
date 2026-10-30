using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Tracking.Domain.Entities;

/// <summary>IoT Device Management: a connected device and the asset it is assigned to (e.g. VS-30018, a Vibration Sensor on Pump PMP-118).</summary>
/// <remarks>
/// Managed independently from assets and tags, so a device can be replaced without changing the asset record.
/// One record per device id. Health is kept in BaseEntity.Status and is one of <see cref="IoTDeviceHealth"/>.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class IoTDevice : BaseEntity
{
    [BsonElement("device_id")]
    public string DeviceId { get; set; } = string.Empty;

    [BsonElement("device_name")]
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>e.g. BLE Tracker, GPS / Cellular, RFID Reader.</summary>
    [BsonElement("device_type")]
    public string DeviceType { get; set; } = string.Empty;

    [BsonElement("model")]
    public string Model { get; set; } = string.Empty;

    /// <summary>One of <see cref="DeviceConnectivity.All"/>.</summary>
    [BsonElement("connectivity")]
    public string Connectivity { get; set; } = string.Empty;

    [BsonElement("asset_registration_id")]
    public string? AssetRegistrationId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    /// <summary>The gateway the device reports through, e.g. GW-DXB-04, or "Cellular".</summary>
    [BsonElement("gateway")]
    public string? Gateway { get; set; }

    /// <summary>One of <see cref="DevicePowerSource.All"/>.</summary>
    [BsonElement("power_source")]
    public string PowerSource { get; set; } = string.Empty;

    /// <summary>Battery charge in percent; only for battery-powered devices.</summary>
    [BsonElement("battery_level")]
    public int? BatteryLevel { get; set; }

    [BsonElement("last_seen_at")]
    public DateTime? LastSeenAt { get; set; }

    /// <summary>Firmware installed on the device, e.g. v1.6.2.</summary>
    [BsonElement("firmware_version")]
    public string? FirmwareVersion { get; set; }

    /// <summary>Firmware the device is scheduled to update to; null when no update is scheduled.</summary>
    [BsonElement("pending_firmware_version")]
    public string? PendingFirmwareVersion { get; set; }

    [BsonElement("firmware_update_scheduled_at")]
    public DateTime? FirmwareUpdateScheduledAt { get; set; }
}

public static class IoTDeviceType
{
    /// <summary>A device that other devices report through, offered in the Gateway dropdown.</summary>
    public const string Gateway = "Gateway";
}

public static class IoTDeviceHealth
{
    public const string Good = "Good";
    public const string Warning = "Warning";
    public const string Critical = "Critical";

    public static readonly IReadOnlyList<string> All = [Good, Warning, Critical];
}
