using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Tracking.Domain.Entities;

/// <summary>
/// A device category in Device Type & Model (e.g. Environmental Sensor), created with the Add Device Type form.
/// Models and devices refer to it by <see cref="Name"/>, which is unique ignoring case.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class DeviceType : BaseEntity
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>One of <see cref="DeviceTypeIcon.All"/>.</summary>
    [BsonElement("icon")]
    public string Icon { get; set; } = DeviceTypeIcon.Chip;

    /// <summary>One of <see cref="DeviceTypeThumbnail.All"/>: how model thumbnails of this type are drawn.</summary>
    [BsonElement("thumbnail_style")]
    public string ThumbnailStyle { get; set; } = DeviceTypeThumbnail.SensorBox;

    [BsonElement("description")]
    public string? Description { get; set; }
}

/// <summary>The icons offered in the Add Device Type form, in the order shown.</summary>
public static class DeviceTypeIcon
{
    public const string Tracker = "tracker";
    public const string Thermometer = "thermometer";
    public const string Pulse = "pulse";
    public const string Gateway = "gateway";
    public const string Scanner = "scanner";
    public const string Bluetooth = "bluetooth";
    public const string Gauge = "gauge";
    public const string People = "people";
    public const string Chip = "chip";

    public static readonly IReadOnlyList<string> All = [Tracker, Thermometer, Pulse, Gateway, Scanner, Bluetooth, Gauge, People, Chip];
}

public static class DeviceTypeThumbnail
{
    public const string SensorBox = "Sensor box";
    public const string Tracker = "Tracker";
    public const string Gateway = "Gateway";
    public const string Reader = "Reader";
    public const string Beacon = "Beacon";
    public const string Meter = "Meter";

    public static readonly IReadOnlyList<string> All = [SensorBox, Tracker, Gateway, Reader, Beacon, Meter];
}

/// <summary>The device types created when the collection is empty (the ones the page started with).</summary>
public static class DeviceTypeDefaults
{
    public static readonly IReadOnlyList<(string Name, string Icon, string Thumbnail, string Description)> All =
    [
        ("Asset Tracker", DeviceTypeIcon.Tracker, DeviceTypeThumbnail.Tracker, "GPS / BLE trackers fixed to movable assets"),
        ("Environmental Sensor", DeviceTypeIcon.Thermometer, DeviceTypeThumbnail.SensorBox, "Temperature, humidity, air quality and similar sensors"),
        ("Condition Sensor", DeviceTypeIcon.Pulse, DeviceTypeThumbnail.SensorBox, "Vibration, current and other machine condition sensors"),
        ("Gateway", DeviceTypeIcon.Gateway, DeviceTypeThumbnail.Gateway, "Gateways that relay readings from other devices"),
        ("RFID Reader", DeviceTypeIcon.Scanner, DeviceTypeThumbnail.Reader, "Fixed and handheld RFID readers and portals"),
        ("BLE Beacon", DeviceTypeIcon.Bluetooth, DeviceTypeThumbnail.Beacon, "Bluetooth Low Energy beacons"),
        ("Smart Meter", DeviceTypeIcon.Gauge, DeviceTypeThumbnail.Meter, "Energy, water and gas meters"),
        ("People Counter", DeviceTypeIcon.People, DeviceTypeThumbnail.SensorBox, "Occupancy and people counting sensors")
    ];
}
