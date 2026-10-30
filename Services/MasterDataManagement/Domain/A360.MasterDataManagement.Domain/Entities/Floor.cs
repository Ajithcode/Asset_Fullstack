using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class Floor : BaseEntity
{
    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Points at a site in this service.
    [BsonElement("site")]
    public MasterDataReference? Site { get; set; }

    [BsonElement("building_id")]
    public string BuildingId { get; set; } = null!;

    // Copied from the building so the floor can be shown without a lookup.
    [BsonElement("building_name")]
    public string BuildingName { get; set; } = null!;

    [BsonElement("parent_level")]
    public string? ParentLevel { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("capacity")]
    public int? Capacity { get; set; }

    [BsonElement("area")]
    public double? Area { get; set; }

    // Main photo (URL or data URL) and extra photos.
    [BsonElement("image_url")]
    public string? ImageUrl { get; set; }

    [BsonElement("images")]
    public List<string> Images { get; set; } = [];

    [BsonElement("allow_asset_assignment")]
    public bool AllowAssetAssignment { get; set; }

    [BsonElement("enable_tracking")]
    public bool EnableTracking { get; set; }

    [BsonElement("restricted_access")]
    public bool RestrictedAccess { get; set; }

    [BsonElement("show_on_map")]
    public bool ShowOnMap { get; set; }

    // 0 = ground floor, negative = basement.
    [BsonElement("level_number")]
    public int? LevelNumber { get; set; }

    // Optional own position; otherwise the building's is used.
    [BsonElement("latitude")]
    public double? Latitude { get; set; }

    [BsonElement("longitude")]
    public double? Longitude { get; set; }
}
