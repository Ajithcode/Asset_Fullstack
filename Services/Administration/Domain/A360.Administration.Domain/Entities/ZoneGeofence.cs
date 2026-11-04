using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

/// <summary>
/// Zones &amp; Geofences: a zone drawn on a floor plan, with its area and perimeter (e.g. Production Zone (PZ-01), 1,280.50 m², 146.20 m).
/// </summary>
/// <remarks>
/// Status (Active or Inactive) is kept in BaseEntity.Status. Zone types and access rules share the Map Library lists
/// (<see cref="MapZoneType"/>, <see cref="MapZoneAccessRule"/>).
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class ZoneGeofence : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("zone_id")]
    public string ZoneId { get; set; } = string.Empty;

    // Floor plan the zone is drawn on, e.g. Building A – Floor 1.
    [BsonElement("floor_plan_id")]
    public string? FloorPlanId { get; set; }

    [BsonElement("floor_plan_name")]
    public string? FloorPlanName { get; set; }

    // e.g. Production Zone.
    [BsonElement("zone_name")]
    public string ZoneName { get; set; } = string.Empty;

    // e.g. PZ-01; unique within a tenant.
    [BsonElement("zone_code")]
    public string ZoneCode { get; set; } = string.Empty;

    // One of MapZoneType.All.
    [BsonElement("zone_type")]
    public string ZoneType { get; set; } = string.Empty;

    [BsonElement("location_id")]
    public string? LocationId { get; set; }

    // Location hierarchy, e.g. Head Office > Building A > Floor 1.
    [BsonElement("location_path")]
    public string? LocationPath { get; set; }

    // Square metres, calculated by the map from the drawn shape.
    [BsonElement("area")]
    public double? Area { get; set; }

    // Metres, calculated by the map from the drawn shape.
    [BsonElement("perimeter")]
    public double? Perimeter { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // One of MapZoneAccessRule.All.
    [BsonElement("access_rule")]
    public string? AccessRule { get; set; }
}
