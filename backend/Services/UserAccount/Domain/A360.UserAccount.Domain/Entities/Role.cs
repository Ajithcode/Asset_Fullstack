using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.UserAccount.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class Role : BaseEntity
{
    [BsonElement("role_id")]
    public string RoleId { get; set; } = string.Empty;

    [BsonElement("role_name")]
    public string RoleName { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

   // [BsonElement("AssignedProject")]
   // public List<AssignedProject> AssignedProjects { get; set; } = [];

    [BsonElement("AssignedPermissions")]
    public List<AssignedPermission> AssignedPermissions { get; set; } = [];

    [BsonElement("Action")]
    public string Action { get; set; } = string.Empty;
}

// public sealed class AssignedProject
// {
//     [BsonElement("project_id")]
//     [BsonRepresentation(BsonType.ObjectId)]
//     public string ProjectId { get; set; } = string.Empty;

//     [BsonElement("project_name")]
//     public string ProjectName { get; set; } = string.Empty;

//     [BsonElement("countryDetail")]
//     public List<CountryDetail> CountryDetails { get; set; } = [];

//     [BsonElement("areaDetail")]
//     public List<AreaDetail> AreaDetails { get; set; } = [];

//     [BsonElement("buildingDetail")]
//     public List<BuildingDetail> BuildingDetails { get; set; } = [];

//     [BsonElement("floorDetail")]
//     public List<FloorDetail> FloorDetails { get; set; } = [];

//     [BsonElement("zoneDetail")]
//     public List<ZoneDetail> ZoneDetails { get; set; } = [];
// }

// public sealed class CountryDetail
// {
//     [BsonElement("country_id")]
//     [BsonRepresentation(BsonType.ObjectId)]
//     public string CountryId { get; set; } = string.Empty;

//     [BsonElement("country_name")]
//     public string CountryName { get; set; } = string.Empty;
// }

// public sealed class AreaDetail
// {
//     [BsonElement("area_id")]
//     [BsonRepresentation(BsonType.ObjectId)]
//     public string AreaId { get; set; } = string.Empty;

//     [BsonElement("area_name")]
//     public string AreaName { get; set; } = string.Empty;
// }

// public sealed class BuildingDetail
// {
//     [BsonElement("building_id")]
//     [BsonRepresentation(BsonType.ObjectId)]
//     public string BuildingId { get; set; } = string.Empty;

//     [BsonElement("building_name")]
//     public string BuildingName { get; set; } = string.Empty;
// }

// public sealed class FloorDetail
// {
//     [BsonElement("floor_id")]
//     [BsonRepresentation(BsonType.ObjectId)]
//     public string FloorId { get; set; } = string.Empty;

//     [BsonElement("floor_name")]
//     public string FloorName { get; set; } = string.Empty;
// }

// public sealed class ZoneDetail
// {
//     [BsonElement("Zone_id")]
//     [BsonRepresentation(BsonType.ObjectId)]
//     public string ZoneId { get; set; } = string.Empty;

//     [BsonElement("Zone_name")]
//     public string ZoneName { get; set; } = string.Empty;
// }

public sealed class AssignedPermission
{
    [BsonElement("featurename")]
    public string FeatureName { get; set; } = string.Empty;

    [BsonElement("viewOption")]
    public bool ViewOption { get; set; }

    [BsonElement("EditOption")]
    public bool EditOption { get; set; }
}
