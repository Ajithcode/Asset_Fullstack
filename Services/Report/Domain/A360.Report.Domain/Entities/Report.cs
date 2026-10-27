using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Report.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class Report : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("report_id")]
    public string ReportId { get; set; } = string.Empty;

    [BsonElement("report_name")]
    public string ReportName { get; set; } = string.Empty;

    [BsonElement("choose_format")]
    public string ChooseFormat { get; set; } = string.Empty;

    [BsonElement("recurrence")]
    public string? Recurrence { get; set; }

    [BsonElement("time_range")]
    public string? TimeRange { get; set; }

    [BsonElement("select_module")]
    public string SelectModule { get; set; } = string.Empty;

    [BsonElement("select_template")]
    public string? SelectTemplate { get; set; }

    [BsonElement("share_with")]
    public List<string> ShareWith { get; set; } = [];
}
