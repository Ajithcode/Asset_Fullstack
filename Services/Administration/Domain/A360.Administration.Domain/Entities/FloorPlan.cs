using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

/// <summary>
/// Floor Plans: an uploaded plan of a location (e.g. Building A - Floor 1, Floor1_v2.1.png, v2.1, Published).
/// </summary>
/// <remarks>Status (Draft, Published or Archived) is kept in BaseEntity.Status.</remarks>
[BsonIgnoreExtraElements]
public sealed class FloorPlan : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("floor_plan_id")]
    public string FloorPlanId { get; set; } = string.Empty;

    // e.g. Building A - Floor 1.
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    // e.g. Main production floor.
    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("location_id")]
    public string? LocationId { get; set; }

    // Location hierarchy, e.g. Head Office > Building A > Floor 1.
    [BsonElement("location_path")]
    public string LocationPath { get; set; } = string.Empty;

    // One of FloorPlanType.All.
    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    // e.g. Floor1_v2.1.png.
    [BsonElement("file_name")]
    public string FileName { get; set; } = string.Empty;

    // Where the uploaded file is stored (Media service URL).
    [BsonElement("file_url")]
    public string? FileUrl { get; set; }

    // PNG, JPG, SVG, PDF, DWG or DXF; taken from the file name when not given.
    [BsonElement("file_format")]
    public string? FileFormat { get; set; }

    [BsonElement("file_size_bytes")]
    public long? FileSizeBytes { get; set; }

    // e.g. v2.1.
    [BsonElement("version")]
    public string Version { get; set; } = string.Empty;

    // Real-world size in metres, e.g. 60.00 m × 32.00 m.
    [BsonElement("width")]
    public double? Width { get; set; }

    [BsonElement("length")]
    public double? Length { get; set; }

    // Drawing scale as 1 : N, e.g. 100 for 1 : 100.
    [BsonElement("scale")]
    public int? Scale { get; set; }

    [BsonElement("published_on")]
    public DateTime? PublishedOn { get; set; }

    [BsonElement("published_by")]
    public string? PublishedBy { get; set; }
}

public static class FloorPlanType
{
    public static readonly IReadOnlyList<string> All =
        ["Floor Plan", "Site Plan", "Building Plan", "Campus Map", "Evacuation Plan"];
}

public static class FloorPlanStatus
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Archived = "Archived";

    public static readonly IReadOnlyList<string> All = [Draft, Published, Archived];
}

public static class FloorPlanFileFormat
{
    public static readonly IReadOnlyList<string> All = ["PNG", "JPG", "JPEG", "SVG", "PDF", "DWG", "DXF"];
}
