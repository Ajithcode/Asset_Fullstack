
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Wip.Domain.Entities;

public class LocationMaster : BaseEntity
{
    [BsonElement("location_id")]
    public string LocationId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string? AssetId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("site")]
    public string? Site { get; set; }

    [BsonElement("building")]
    public string? Building { get; set; }

    [BsonElement("floor")]
    public string? Floor { get; set; }

    [BsonElement("zone")]
    public string? Zone { get; set; }

    [BsonElement("geo_coordinates")]
    public string? GeoCoordinates { get; set; }

    [BsonElement("parent_location")]
    public string? ParentLocation { get; set; }
}
