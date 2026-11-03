using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Tracking.Domain.Entities;

/// <summary>Device Replacement → Replacement Device: a spare device that can take over from a failed one (e.g. BLE-30112, Minew E8 at Central Store).</summary>
/// <remarks>One record per device id. Availability is kept in BaseEntity.Status and is one of <see cref="ReplacementAvailability"/>.</remarks>
[BsonIgnoreExtraElements]
public sealed class ReplacementDevice : BaseEntity
{
    [BsonElement("device_id")]
    public string DeviceId { get; set; } = string.Empty;

    [BsonElement("model")]
    public string Model { get; set; } = string.Empty;

    /// <summary>One of <see cref="ReplacementCompatibility.All"/>.</summary>
    [BsonElement("compatibility")]
    public string Compatibility { get; set; } = string.Empty;

    /// <summary>Protocol and version, e.g. BLE 5.0.</summary>
    [BsonElement("protocol")]
    public string? Protocol { get; set; }

    /// <summary>Battery charge in percent; null for mains-powered devices.</summary>
    [BsonElement("battery_level")]
    public int? BatteryLevel { get; set; }

    /// <summary>Where the spare is kept, e.g. Central Store.</summary>
    [BsonElement("location_name")]
    public string LocationName { get; set; } = string.Empty;
}

public static class ReplacementAvailability
{
    public const string Available = "Available";
    public const string Reserved = "Reserved";
    public const string Unavailable = "Unavailable";

    public static readonly IReadOnlyList<string> All = [Available, Reserved, Unavailable];
}

public static class ReplacementCompatibility
{
    public const string Compatible = "Compatible";
    public const string Partial = "Partial";
    public const string Incompatible = "Incompatible";

    public static readonly IReadOnlyList<string> All = [Compatible, Partial, Incompatible];
}
