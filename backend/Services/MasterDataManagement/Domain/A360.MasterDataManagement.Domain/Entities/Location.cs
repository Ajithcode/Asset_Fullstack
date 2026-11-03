using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class Location : BaseEntity
{
    [BsonElement("location_name")]
    public string LocationName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    [BsonElement("type")]
    public string Type { get; set; } = null!;

    [BsonElement("building")]
    public string? Building { get; set; }

    [BsonElement("floor")]
    public string? Floor { get; set; }

    [BsonElement("zone")]
    public string? Zone { get; set; }

    [BsonElement("parent_location_id")]
    public string? ParentLocationId { get; set; }

    // Denormalized so lists can show the parent without a second lookup; kept in sync on rename.
    [BsonElement("parent_location_name")]
    public string? ParentLocationName { get; set; }

    // Indoor or Outdoor.
    [BsonElement("environment")]
    public string? Environment { get; set; }

    // Site in this service; Building, Floor and Zone hold the codes of those records.
    [BsonElement("site")]
    public MasterDataReference? Site { get; set; }

    [BsonElement("area")]
    public double? Area { get; set; }

    [BsonElement("capacity")]
    public int? Capacity { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // Photo (URL or data URL).
    [BsonElement("image_url")]
    public string? ImageUrl { get; set; }

    [BsonElement("latitude")]
    public double? Latitude { get; set; }

    [BsonElement("longitude")]
    public double? Longitude { get; set; }

    [BsonElement("restricted_access")]
    public bool RestrictedAccess { get; set; }

    [BsonElement("temperature_controlled")]
    public bool TemperatureControlled { get; set; }

    [BsonElement("allow_asset_assignment")]
    public bool AllowAssetAssignment { get; set; }

    [BsonElement("enable_tracking")]
    public bool EnableTracking { get; set; }

    [BsonElement("geofence_alerts")]
    public bool GeofenceAlerts { get; set; }
}
