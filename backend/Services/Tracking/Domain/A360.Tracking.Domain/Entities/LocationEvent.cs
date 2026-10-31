using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Tracking.Domain.Entities;

/// <summary>Real-Time Location → Recent Location Events: one zone event of a tracked asset (e.g. LE000001, FL-008 entered Warehouse A).</summary>
/// <remarks>Events are a log, so the same asset can have many with the same type and zone; each gets its own EventId.</remarks>
[BsonIgnoreExtraElements]
public sealed class LocationEvent : BaseEntity
{
    [BsonElement("event_id")]
    public string EventId { get; set; } = string.Empty;

    [BsonElement("event_time")]
    public DateTime EventTime { get; set; }

    /// <summary>The asset as shown on the map, e.g. FL-008.</summary>
    [BsonElement("asset_id")]
    public string AssetId { get; set; } = string.Empty;

    [BsonElement("asset_name")]
    public string AssetName { get; set; } = string.Empty;

    /// <summary>One of <see cref="LocationEventType.All"/>.</summary>
    [BsonElement("event_type")]
    public string EventType { get; set; } = string.Empty;

    [BsonElement("zone_name")]
    public string ZoneName { get; set; } = string.Empty;

    /// <summary>e.g. "Departed Charging Zone (dwell time 18 min)".</summary>
    [BsonElement("details")]
    public string? Details { get; set; }
}

public static class LocationEventType
{
    public const string EnteredZone = "Entered Zone";
    public const string DepartedZone = "Departed Zone";
    public const string GeofenceAlert = "Geofence Alert";
    public const string DwellExceeded = "Dwell Exceeded";

    public static readonly IReadOnlyList<string> All = [EnteredZone, DepartedZone, GeofenceAlert, DwellExceeded];
}
