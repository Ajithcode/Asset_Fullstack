using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Tracking.Domain.Entities;

/// <summary>
/// IoT Tracking Dashboard → Utilization Rate: how long one asset was running and idle on one day
/// (e.g. UT000001, DG-250, Generator, 2026-11-01, 14 h running / 10 h idle).
/// </summary>
/// <remarks>One record per asset per day; reported by the device or entered manually.</remarks>
[BsonIgnoreExtraElements]
public sealed class AssetUtilization : BaseEntity
{
    [BsonElement("utilization_id")]
    public string UtilizationId { get; set; } = string.Empty;

    /// <summary>The day the hours belong to (UTC date, time is 00:00).</summary>
    [BsonElement("date")]
    public DateTime Date { get; set; }

    [BsonElement("asset_registration_id")]
    public string AssetRegistrationId { get; set; } = string.Empty;

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    /// <summary>e.g. Generator, Forklift, Chiller; the bars on the dashboard are grouped by this.</summary>
    [BsonElement("asset_type")]
    public string AssetType { get; set; } = string.Empty;

    [BsonElement("device_id")]
    public string? DeviceId { get; set; }

    [BsonElement("site_name")]
    public string? SiteName { get; set; }

    [BsonElement("running_minutes")]
    public int RunningMinutes { get; set; }

    [BsonElement("idle_minutes")]
    public int IdleMinutes { get; set; }
}
