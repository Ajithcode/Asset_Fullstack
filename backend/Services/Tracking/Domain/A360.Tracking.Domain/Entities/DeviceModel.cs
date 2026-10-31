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

    /// <summary>One of <see cref="DevicePowerSource.All"/>.</summary>
    [BsonElement("power_source")]
    public string PowerSource { get; set; } = string.Empty;

    /// <summary>Protocol and version, e.g. LoRaWAN 1.0.3, BLE 5.0.</summary>
    [BsonElement("protocol")]
    public string? Protocol { get; set; }
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
