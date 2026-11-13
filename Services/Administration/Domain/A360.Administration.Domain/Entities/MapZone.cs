using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

// An area drawn on a floor plan. Positions are percent of the plan (0-100).
// Status (Active, Inactive) is stored in BaseEntity.Status and follows Active.
[BsonIgnoreExtraElements]
public sealed class MapZone : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("zone_id")]
    public string ZoneId { get; set; } = string.Empty;

    [BsonElement("floor_plan_id")]
    public string FloorPlanId { get; set; } = string.Empty;

    // Unique per floor plan, e.g. PZ-01.
    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    // Production, Office, Storage, Restricted, Maintenance or Operational.
    [BsonElement("zone_type")]
    public string ZoneType { get; set; } = string.Empty;

    [BsonElement("color")]
    public string? Color { get; set; }

    // Polygon, Rectangle, Circle or Custom.
    [BsonElement("shape")]
    public string Shape { get; set; } = string.Empty;

    // Bounding box.
    [BsonElement("x")]
    public double X { get; set; }

    [BsonElement("y")]
    public double Y { get; set; }

    [BsonElement("w")]
    public double W { get; set; }

    [BsonElement("h")]
    public double H { get; set; }

    // Outline for Polygon / Custom shapes.
    [BsonElement("points")]
    public List<MapPoint> Points { get; set; } = [];

    // Circle radius, percent of the plan width.
    [BsonElement("radius")]
    public double? Radius { get; set; }

    [BsonElement("area_m2")]
    public double? AreaM2 { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("access_rule")]
    public string? AccessRule { get; set; }

    [BsonElement("active")]
    public bool Active { get; set; } = true;

    // Geofence alerting.
    [BsonElement("geofence")]
    public bool Geofence { get; set; }

    [BsonElement("entry_alert")]
    public bool EntryAlert { get; set; }

    [BsonElement("exit_alert")]
    public bool ExitAlert { get; set; }

    [BsonElement("dwell_alert")]
    public bool DwellAlert { get; set; }

    [BsonElement("dwell_minutes")]
    public int DwellMinutes { get; set; }

    // Site > building > floor > Master Data zone of the drawn zone (set by the server on save).
    [BsonElement("location")]
    public MapLocation? Location { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class MapPoint
{
    [BsonElement("x")]
    public double X { get; set; }

    [BsonElement("y")]
    public double Y { get; set; }
}
