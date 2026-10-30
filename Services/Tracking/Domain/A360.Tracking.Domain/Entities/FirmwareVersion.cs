using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Tracking.Domain.Entities;

/// <summary>Firmware & Connectivity → Firmware Inventory: the firmware a device model runs against the latest release (e.g. Milesight EM300-TH, v1.6.2 → v1.7.0).</summary>
/// <remarks>One row per device model. The update status is kept in BaseEntity.Status and is one of <see cref="FirmwareUpdateStatus"/>.</remarks>
[BsonIgnoreExtraElements]
public sealed class FirmwareVersion : BaseEntity
{
    [BsonElement("device_model")]
    public string DeviceModel { get; set; } = string.Empty;

    [BsonElement("current_version")]
    public string CurrentVersion { get; set; } = string.Empty;

    [BsonElement("latest_version")]
    public string LatestVersion { get; set; } = string.Empty;

    /// <summary>How many devices of this model are in the fleet.</summary>
    [BsonElement("device_count")]
    public int DeviceCount { get; set; }

    /// <summary>What changed in the latest version, one line per note.</summary>
    [BsonElement("release_notes")]
    public List<string> ReleaseNotes { get; set; } = [];

    /// <summary>Release date of the latest version.</summary>
    [BsonElement("release_date")]
    public DateTime? ReleaseDate { get; set; }
}

public static class FirmwareUpdateStatus
{
    public const string UpToDate = "Up to date";
    public const string UpdateAvailable = "Update available";

    public static readonly IReadOnlyList<string> All = [UpToDate, UpdateAvailable];
}
