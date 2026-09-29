using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Locating.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class DeviceDrawing : BaseEntity
{
    [BsonElement("project_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ProjectId { get; set; } = string.Empty;

    [BsonElement("country_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string CountryId { get; set; } = string.Empty;

    [BsonElement("area_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string AreaId { get; set; } = string.Empty;

    [BsonElement("building_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string BuildingId { get; set; } = string.Empty;

    [BsonElement("floor_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string FloorId { get; set; } = string.Empty;

    [BsonElement("zone_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ZoneId { get; set; } = string.Empty;

    [BsonElement("zone_name")]
    public string ZoneName { get; set; } = string.Empty;

    [BsonElement("top_zone")]
    public string? TopZone { get; set; }

    [BsonElement("device_reference_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string DeviceReferenceId { get; set; } = string.Empty;

    [BsonElement("device_unique_id")]
    public string DeviceUniqueId { get; set; } = string.Empty;

    [BsonElement("device_name")]
    public string DeviceName { get; set; } = string.Empty;

    [BsonElement("user_id")]
    public string? UserId { get; set; }

    [BsonElement("priority")]
    public string? Priority { get; set; }

    [BsonElement("assembly_point")]
    public bool AssemblyPoint { get; set; }

    [BsonElement("exit")]
    public string? Exit { get; set; }

    [BsonElement("is_active")]
    public new bool Status { get; set; }

    [BsonElement("device_geo_json_data")]
    public GeoJsonFeatureCollection DeviceGeoJsonData { get; set; } = new();
}
