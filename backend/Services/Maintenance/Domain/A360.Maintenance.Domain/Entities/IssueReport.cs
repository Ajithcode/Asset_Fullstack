
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Maintenance.Domain.Entities;

public class IssueReport : BaseEntity
{
    [BsonElement("issue_id")]
    public string IssueId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string AssetId { get; set; } = null!;

    [BsonElement("asset_name")]
    public string AssetName { get; set; } = null!;

    [BsonElement("reported_by")]
    public string? ReportedBy { get; set; }

    [BsonElement("issue_type")]
    public string IssueType { get; set; } = null!;

    [BsonElement("severity")]
    public string Severity { get; set; } = null!;

    [BsonElement("description")]
    public string? Description { get; set; }
}
