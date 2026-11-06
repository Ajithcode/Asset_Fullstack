using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

// A floor plan or site map drawn over an uploaded file. Status (Draft, Published, Archived) is stored in BaseEntity.Status.
// Every uploaded revision is kept in Versions; the plan's file fields mirror the current version.
[BsonIgnoreExtraElements]
public sealed class FloorPlan : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("floor_plan_id")]
    public string FloorPlanId { get; set; } = string.Empty;

    // Master Data Management id of the site, building or floor the plan belongs to.
    [BsonElement("location_id")]
    public string LocationId { get; set; } = string.Empty;

    // site, building or floor.
    [BsonElement("location_type")]
    public string LocationType { get; set; } = string.Empty;

    // Display text such as "Head Office > Building A > Floor 1".
    [BsonElement("location_path")]
    public string? LocationPath { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; }

    // Floor Plan or Site Map.
    [BsonElement("plan_type")]
    public string PlanType { get; set; } = string.Empty;

    // PNG, JPG, PDF, SVG, DWG or DXF.
    [BsonElement("format")]
    public string Format { get; set; } = string.Empty;

    // URL returned by the Media service upload; may be empty when no file service is used.
    [BsonElement("file_url")]
    public string? FileUrl { get; set; }

    [BsonElement("file_name")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("size_bytes")]
    public long SizeBytes { get; set; }

    // Real-world size of the drawing, used to turn plan percentages into meters.
    [BsonElement("width_meters")]
    public double? WidthMeters { get; set; }

    [BsonElement("height_meters")]
    public double? HeightMeters { get; set; }

    // Drawing scale such as 1:100.
    [BsonElement("scale")]
    public string? Scale { get; set; }

    // Version shown on the plan, e.g. v1.0.
    [BsonElement("current_version")]
    public string CurrentVersion { get; set; } = string.Empty;

    [BsonElement("published_at")]
    public DateTime? PublishedAt { get; set; }

    [BsonElement("published_by")]
    public string? PublishedBy { get; set; }

    [BsonElement("versions")]
    public List<FloorPlanVersion> Versions { get; set; } = [];
}

// One uploaded revision of a floor plan, numbered vMAJOR.MINOR.
[BsonIgnoreExtraElements]
public sealed class FloorPlanVersion
{
    [BsonElement("version")]
    public string Version { get; set; } = string.Empty;

    // Draft, Published or Archived.
    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    [BsonElement("file_url")]
    public string? FileUrl { get; set; }

    [BsonElement("file_name")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("format")]
    public string Format { get; set; } = string.Empty;

    [BsonElement("size_bytes")]
    public long SizeBytes { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // Change notes listed in the version history.
    [BsonElement("changes")]
    public List<string> Changes { get; set; } = [];

    [BsonElement("uploaded_by")]
    public string? UploadedBy { get; set; }

    [BsonElement("uploaded_at")]
    public DateTime? UploadedAt { get; set; }

    [BsonElement("published_at")]
    public DateTime? PublishedAt { get; set; }

    [BsonElement("published_by")]
    public string? PublishedBy { get; set; }
}
