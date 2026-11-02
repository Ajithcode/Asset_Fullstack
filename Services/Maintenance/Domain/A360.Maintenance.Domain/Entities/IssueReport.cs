using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Maintenance.Domain.Entities;

/// <summary>Service request raised against an asset or facility (shown as Service Requests in the UI).</summary>
[BsonIgnoreExtraElements]
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

    /// <summary>Request type: Repair Request, Service Request, Spare Part Request, Inspection Request.</summary>
    [BsonElement("issue_type")]
    public string IssueType { get; set; } = null!;

    /// <summary>Priority: High, Medium, Low.</summary>
    [BsonElement("severity")]
    public string Severity { get; set; } = null!;

    [BsonElement("description")]
    public string? Description { get; set; }

    /// <summary>Equipment Issue, Facility Issue, Spare Part Request, Inspection Request, Other.</summary>
    [BsonElement("category")]
    public string? Category { get; set; }

    [BsonElement("location")]
    public string? Location { get; set; }

    [BsonElement("requested_date")]
    public DateTime? RequestedDate { get; set; }

    [BsonElement("target_date")]
    public DateTime? TargetDate { get; set; }

    [BsonElement("assigned_to")]
    public string? AssignedTo { get; set; }

    /// <summary>Work order raised from this request, if any.</summary>
    [BsonElement("work_order_id")]
    public string? WorkOrderId { get; set; }

    [BsonElement("resolved_date")]
    public DateTime? ResolvedDate { get; set; }

    [BsonElement("resolution_notes")]
    public string? ResolutionNotes { get; set; }
}
