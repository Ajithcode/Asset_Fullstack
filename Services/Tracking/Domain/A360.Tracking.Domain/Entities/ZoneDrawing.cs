using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Tracking.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class ZoneDrawing : BaseEntity
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

    [BsonElement("priority")]
    public string? Priority { get; set; }

    [BsonElement("assembly_point")]
    public bool AssemblyPoint { get; set; }

    [BsonElement("exit")]
    public string? Exit { get; set; }

    [BsonElement("is_active")]
    public new bool Status { get; set; }

    [BsonElement("geo_json_data")]
    public GeoJsonFeatureCollection GeoJsonData { get; set; } = new();
}

public sealed class GeoJsonFeatureCollection
{
    [BsonElement("type")]
    public string Type { get; set; } = "FeatureCollection";

    [BsonElement("features")]
    public List<GeoJsonFeature> Features { get; set; } = [];
}

public sealed class GeoJsonFeature
{
    [BsonElement("type")]
    public string Type { get; set; } = "Feature";

    [BsonElement("geometry")]
    public GeoJsonGeometry Geometry { get; set; } = new();

    [BsonElement("properties")]
    public BsonDocument Properties { get; set; } = [];
}

public sealed class GeoJsonGeometry
{
    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    // Nesting depth depends on the geometry type (Point, Polygon, MultiPolygon, ...), so it is kept as a raw array.
    [BsonElement("coordinates")]
    public BsonArray Coordinates { get; set; } = [];
}
