using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Tracking.Domain.Entities;

/// <summary>Device Health & Status: the latest connectivity, signal, power and health of one connected device (e.g. VS-30018, Vibration Sensor V2).</summary>
/// <remarks>One record per device id. Health is kept in BaseEntity.Status and is one of <see cref="DeviceHealthStatus"/>.</remarks>
[BsonIgnoreExtraElements]
public sealed class DeviceHealth : BaseEntity
{
    [BsonElement("device_id")]
    public string DeviceId { get; set; } = string.Empty;

    [BsonElement("device_model")]
    public string DeviceModel { get; set; } = string.Empty;

    [BsonElement("asset_registration_id")]
    public string? AssetRegistrationId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("site_id")]
    public string? SiteId { get; set; }

    [BsonElement("site_name")]
    public string? SiteName { get; set; }

    /// <summary>The gateway the device reports through, e.g. GW-DXB-04, or "Cellular".</summary>
    [BsonElement("gateway")]
    public string? Gateway { get; set; }

    /// <summary>One of <see cref="DeviceConnectivity.All"/>.</summary>
    [BsonElement("connectivity")]
    public string Connectivity { get; set; } = string.Empty;

    /// <summary>RSSI in dBm, e.g. -61; null when the device does not report it.</summary>
    [BsonElement("signal_strength_dbm")]
    public int? SignalStrengthDbm { get; set; }

    /// <summary>One of <see cref="SignalQuality.All"/>.</summary>
    [BsonElement("signal_quality")]
    public string? SignalQuality { get; set; }

    /// <summary>One of <see cref="DevicePowerSource.All"/>.</summary>
    [BsonElement("power_source")]
    public string PowerSource { get; set; } = string.Empty;

    /// <summary>Battery charge in percent; only for battery-powered devices.</summary>
    [BsonElement("battery_level")]
    public int? BatteryLevel { get; set; }

    [BsonElement("last_seen_at")]
    public DateTime? LastSeenAt { get; set; }
}

public static class DeviceConnectivity
{
    public const string Online = "Online";
    public const string Offline = "Offline";
    public const string Warning = "Warning";

    public static readonly IReadOnlyList<string> All = [Online, Offline, Warning];
}

public static class SignalQuality
{
    public const string Strong = "Strong";
    public const string Good = "Good";
    public const string Fair = "Fair";
    public const string Poor = "Poor";

    public static readonly IReadOnlyList<string> All = [Strong, Good, Fair, Poor];

    /// <summary>Quality for an RSSI reading: -61 dBm is Good, -74 / -82 dBm Fair, -96 dBm Poor.</summary>
    public static string FromDbm(int dbm) => dbm switch
    {
        > -70 => Good,
        >= -85 => Fair,
        _ => Poor
    };
}

public static class DevicePowerSource
{
    public const string Battery = "Battery";
    public const string ExternalPower = "External power";
    public const string PoE = "PoE";

    public static readonly IReadOnlyList<string> All = [Battery, ExternalPower, PoE];
}

public static class DeviceHealthStatus
{
    public const string Healthy = "Healthy";
    public const string Attention = "Attention";
    public const string Critical = "Critical";
    public const string Maintenance = "Maintenance";

    public static readonly IReadOnlyList<string> All = [Healthy, Attention, Critical, Maintenance];
}
