using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class Site : BaseEntity
{
    [BsonElement("site_name")]
    public string SiteName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Both point at records in this service.
    [BsonElement("business_unit")]
    public MasterDataReference? BusinessUnit { get; set; }

    [BsonElement("department")]
    public MasterDataReference? Department { get; set; }

    [BsonElement("site_type")]
    public string? SiteType { get; set; }

    [BsonElement("city")]
    public string? City { get; set; }

    [BsonElement("country")]
    public string? Country { get; set; }

    // Owning organization in this service.
    [BsonElement("organization")]
    public MasterDataReference? Organization { get; set; }

    // Optional parent site (in this collection).
    [BsonElement("parent_site")]
    public MasterDataReference? ParentSite { get; set; }

    [BsonElement("time_zone")]
    public string? TimeZone { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // Main photo (URL or data URL) and extra photos.
    [BsonElement("image_url")]
    public string? ImageUrl { get; set; }

    [BsonElement("images")]
    public List<string> Images { get; set; } = [];

    // Full address; City and Country above repeat its city and country for filtering.
    [BsonElement("address")]
    public StructureAddress? Address { get; set; }

    [BsonElement("latitude")]
    public double? Latitude { get; set; }

    [BsonElement("longitude")]
    public double? Longitude { get; set; }

    [BsonElement("manager")]
    public StructureContact? Manager { get; set; }

    [BsonElement("emergency")]
    public StructureContact? Emergency { get; set; }

    // 'HH:mm'
    [BsonElement("open_time")]
    public string? OpenTime { get; set; }

    [BsonElement("close_time")]
    public string? CloseTime { get; set; }

    // e.g. Mon, Tue
    [BsonElement("working_days")]
    public List<string> WorkingDays { get; set; } = [];

    [BsonElement("open_24x7")]
    public bool Open24x7 { get; set; }

    [BsonElement("allow_asset_assignment")]
    public bool AllowAssetAssignment { get; set; }

    [BsonElement("enable_tracking")]
    public bool EnableTracking { get; set; }
}
