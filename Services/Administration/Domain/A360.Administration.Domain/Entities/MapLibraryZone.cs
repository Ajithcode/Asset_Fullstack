using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

/// <summary>
/// Map Library: a zone drawn on a floor plan (e.g. Production Zone (PZ-01) on Head Office > Building A > Floor 1).
/// </summary>
/// <remarks>Status (Active or Inactive) is kept in BaseEntity.Status.</remarks>
[BsonIgnoreExtraElements]
public sealed class MapLibraryZone : BaseEntity
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

    [BsonElement("parent_location_id")]
    public string? ParentLocationId { get; set; }

    // Location hierarchy, e.g. Head Office > Building A > Floor 1.
    [BsonElement("parent_location_path")]
    public string? ParentLocationPath { get; set; }

    // Area in square metres, calculated by the map from the drawn shape.
    [BsonElement("area")]
    public double? Area { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // One of MapZoneAccessRule.All.
    [BsonElement("access_rule")]
    public string? AccessRule { get; set; }
}

public static class MapZoneType
{
    public static readonly IReadOnlyList<string> All =
        ["Operational", "Production", "Office", "Storage", "Restricted", "Maintenance", "Utility", "Common Area", "Parking", "Other"];
}

public static class MapZoneAccessRule
{
    public static readonly IReadOnlyList<string> All =
        ["Open Access", "Authorized Personnel Only", "Restricted Access", "Escort Required", "No Access"];
}

public static class MapZoneStatus
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";

    public static readonly IReadOnlyList<string> All = [Active, Inactive];
}
