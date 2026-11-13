using MongoDB.Bson.Serialization.Attributes;

namespace A360.Administration.Domain.Entities;

// Where a drawn zone or a plotted item sits in the Master Data Management hierarchy: site > building > floor > zone.
// Ids are the Master Data records' Mongo ids (ObjectId strings); names and codes are copied when the zone / plot is
// saved so reports can read them without a lookup. Filled by the server from the floor plan's location.
[BsonIgnoreExtraElements]
public sealed class MapLocation
{
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

    // Master Data zone (master_zones), matched by code on the floor; the drawn outline is in maps_zones.
    [BsonElement("zone_id")]
    public string? ZoneId { get; set; }

    [BsonElement("zone_name")]
    public string? ZoneName { get; set; }

    [BsonElement("zone_code")]
    public string? ZoneCode { get; set; }

    // "Building A1 > BuildingA2 > Floor2 > zone1"
    [BsonElement("location_path")]
    public string? LocationPath { get; set; }

    public MapLocation Copy() => (MapLocation)MemberwiseClone();
}
