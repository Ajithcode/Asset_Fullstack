using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

// A chart type available in dashboards and reports.
public class ChartType : BaseEntity
{
    [BsonElement("chart_type_name")]
    public string ChartTypeName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // e.g. "Comparison", "Trend" or "Distribution".
    [BsonElement("category")]
    public string Category { get; set; } = null!;

    [BsonElement("description")]
    public string? Description { get; set; }

    // URL of a preview image uploaded through /api/media/upload.
    [BsonElement("icon_url")]
    public string? IconUrl { get; set; }
}
