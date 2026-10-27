using MongoDB.Bson.Serialization.Attributes;

namespace A360.Asset.Domain.Entities;

/// <summary>Step 3 of asset registration: where the asset is (location) and who is responsible for it (assignment).</summary>
[BsonIgnoreExtraElements]
public sealed class AssetLocationAssignment
{
    [BsonElement("location")]
    public AssetLocation Location { get; set; } = new();

    [BsonElement("assignment")]
    public AssetAssignment Assignment { get; set; } = new();
}

/// <remarks>
/// Levels come from the Project service hierarchy (Site = Area, Room = SubZone).
/// Each keeps its id for linking and floor plans, plus the name as it was when registered.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class AssetLocation
{
    [BsonElement("project_id")]
    public string? ProjectId { get; set; }

    [BsonElement("country_id")]
    public string? CountryId { get; set; }

    [BsonElement("country_name")]
    public string? CountryName { get; set; }

    [BsonElement("site_id")]
    public string? SiteId { get; set; }

    [BsonElement("site_name")]
    public string? SiteName { get; set; }

    [BsonElement("building_id")]
    public string? BuildingId { get; set; }

    [BsonElement("building_name")]
    public string? BuildingName { get; set; }

    [BsonElement("floor_id")]
    public string? FloorId { get; set; }

    [BsonElement("floor_name")]
    public string? FloorName { get; set; }

    [BsonElement("zone_id")]
    public string? ZoneId { get; set; }

    [BsonElement("zone_name")]
    public string? ZoneName { get; set; }

    [BsonElement("room_id")]
    public string? RoomId { get; set; }

    [BsonElement("room_name")]
    public string? RoomName { get; set; }

    // Built on the server from the level names, e.g. "UAE > Oman > Street One > Third Right > Second Colony".
    [BsonElement("location_path")]
    public string? LocationPath { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class AssetAssignment
{
    [BsonElement("custodian_department")]
    public string? CustodianDepartment { get; set; }

    [BsonElement("assigned_to_user_id")]
    public string? AssignedToUserId { get; set; }

    [BsonElement("assigned_to_name")]
    public string? AssignedToName { get; set; }

    [BsonElement("ownership_type")]
    public string? OwnershipType { get; set; }

    [BsonElement("cost_center")]
    public string? CostCenter { get; set; }

    [BsonElement("responsible_team")]
    public string? ResponsibleTeam { get; set; }

    [BsonElement("assignment_start_date")]
    public DateTime? AssignmentStartDate { get; set; }

    [BsonElement("assignment_end_date")]
    public DateTime? AssignmentEndDate { get; set; }

    [BsonElement("is_current")]
    public bool IsCurrent { get; set; }
}
