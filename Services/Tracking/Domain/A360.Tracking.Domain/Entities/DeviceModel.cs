using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Tracking.Domain.Entities;

/// <summary>Device Type & Model: a reusable model specification (e.g. EM300-TH by Milesight, an Environmental Sensor).</summary>
/// <remarks>
/// Model numbers are unique, so a model can be looked up by its number alone. Each deployed device stays a separate record.
/// Status is kept in BaseEntity.Status and is one of <see cref="DeviceModelStatus"/>.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class DeviceModel : BaseEntity
{
    [BsonElement("model_number")]
    public string ModelNumber { get; set; } = string.Empty;

    [BsonElement("manufacturer")]
    public string Manufacturer { get; set; } = string.Empty;

    /// <summary>Category, e.g. Asset Tracker, Environmental Sensor, Gateway.</summary>
    [BsonElement("device_type")]
    public string DeviceType { get; set; } = string.Empty;

    /// <summary>One of <see cref="DeviceModelConnectivity.All"/>.</summary>
    [BsonElement("connectivity")]
    public string Connectivity { get; set; } = string.Empty;

    /// <summary>e.g. Temperature, Humidity.</summary>
    [BsonElement("supported_measurements")]
    public List<string> SupportedMeasurements { get; set; } = [];

    /// <summary>One of <see cref="DeviceModelPowerSource.All"/>; older models may hold a <see cref="DevicePowerSource"/> value.</summary>
    [BsonElement("power_source")]
    public string PowerSource { get; set; } = string.Empty;

    /// <summary>Protocol and version, e.g. LoRaWAN 1.0.3, BLE 5.0.</summary>
    [BsonElement("protocol")]
    public string? Protocol { get; set; }

    /// <summary>e.g. EU868, US915.</summary>
    [BsonElement("frequency_bands")]
    public List<string> FrequencyBands { get; set; } = [];

    /// <summary>e.g. IP65.</summary>
    [BsonElement("ip_rating")]
    public string? IpRating { get; set; }

    /// <summary>Free text, e.g. Up to 5 years (10 min interval).</summary>
    [BsonElement("battery_life")]
    public string? BatteryLife { get; set; }

    /// <summary>Free text, e.g. -20 °C to 60 °C.</summary>
    [BsonElement("operating_temperature")]
    public string? OperatingTemperature { get; set; }

    /// <summary>Lowest compatible firmware version, e.g. 1.1.0.</summary>
    [BsonElement("firmware_from")]
    public string? FirmwareFrom { get; set; }

    /// <summary>Highest compatible firmware version, e.g. 1.4.0.</summary>
    [BsonElement("firmware_to")]
    public string? FirmwareTo { get; set; }

    /// <summary>Settings a device of this model exposes, e.g. Reporting Interval, Threshold Alerts.</summary>
    [BsonElement("configurable_attributes")]
    public List<string> ConfigurableAttributes { get; set; } = [];
}

/// <summary>The device type categories listed in Device Type & Model, including ones with no models yet.</summary>
public static class DeviceModelType
{
    public static readonly IReadOnlyList<string> All =
    [
        "Asset Tracker", "Environmental Sensor", "Condition Sensor", "Gateway",
        "RFID Reader", "BLE Beacon", "Smart Meter", "People Counter"
    ];
}

/// <summary>Power options of a model; separate from <see cref="DevicePowerSource"/>, which device battery checks rely on.</summary>
public static class DeviceModelPowerSource
{
    public const string BatteryReplaceable = "Battery (replaceable)";
    public const string BatteryRechargeable = "Battery (rechargeable)";
    public const string ExternalPower = "External power";
    public const string PoE = "PoE";

    public static readonly IReadOnlyList<string> All = [BatteryReplaceable, BatteryRechargeable, ExternalPower, PoE];
}

/// <summary>A measurement a model can report, with its unit, e.g. Temperature in °C.</summary>
public sealed record DeviceMeasurement(string Name, string Unit)
{
    public static readonly IReadOnlyList<DeviceMeasurement> All =
    [
        new("Temperature", "°C"), new("Humidity", "%RH"), new("Vibration", "mm/s"), new("Battery", "%"),
        new("Atmospheric Pressure", "hPa"), new("CO₂", "ppm"), new("TVOC", "ppb"), new("Light", "lux"),
        new("Door/Window State", "open/closed"), new("Water Leak", "yes/no"), new("Tilt", "°"), new("Location", "GNSS"),
        new("Motion", "g"), new("Tag Reads", "reads/s"), new("RSSI", "dBm"), new("Uplink Packets", "pkt/min"),
        new("Energy", "kWh"), new("People Count", "count")
    ];
}

public static class DeviceModelAttribute
{
    public static readonly IReadOnlyList<string> All =
    [
        "Reporting Interval", "Measurement Intervals", "Threshold Alerts", "Motion Detection", "Geo-fencing",
        "LED Indication", "Data Retransmission", "OTA Firmware", "Time Sync", "Tamper Alert", "Power Saving Mode"
    ];
}

public static class DeviceModelConnectivity
{
    public const string LoRaWan = "LoRaWAN";
    public const string Ble = "BLE";
    public const string WiFi = "Wi-Fi";
    public const string Cellular = "Cellular";
    public const string Ethernet = "Ethernet";

    public static readonly IReadOnlyList<string> All = [LoRaWan, Ble, WiFi, Cellular, Ethernet];
}

public static class DeviceModelStatus
{
    public const string Draft = "Draft";
    public const string PendingReview = "Pending Review";
    public const string Active = "Active";
    public const string Inactive = "Inactive";

    public static readonly IReadOnlyList<string> All = [Draft, PendingReview, Active, Inactive];
}
