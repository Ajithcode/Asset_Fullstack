using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>A request for service on one asset (repair, inspection…), raised from the asset's Service Requests page.</summary>
/// <remarks>
/// Holds the New Service Request form, plus the Updates, Attachments and Related tabs.
/// Status is BaseEntity.Status: Draft, Open, In Progress, On Hold, Approved, Closed or Cancelled.
/// Asset, person and work order names are copied when saved, so the request can be shown without loading them.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class AssetServiceRequest : BaseEntity
{
    // e.g. "SR-2025-028".
    [BsonElement("request_number")]
    public string RequestNumber { get; set; } = string.Empty;

    [BsonElement("asset")]
    public AssetServiceRequestAsset Asset { get; set; } = new();

    // Request Information.
    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    // e.g. "Corrective", "Preventive", "Inspection".
    [BsonElement("request_type")]
    public string? RequestType { get; set; }

    // "Low", "Medium", "High" or "Critical".
    [BsonElement("priority")]
    public string? Priority { get; set; }

    [BsonElement("requested_date")]
    public DateTime? RequestedDate { get; set; }

    [BsonElement("target_date")]
    public DateTime? TargetDate { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // Location & Assignment.
    [BsonElement("location")]
    public AssetServiceRequestReference? Location { get; set; }

    [BsonElement("department")]
    public AssetServiceRequestReference? Department { get; set; }

    [BsonElement("requested_by")]
    public AssetServiceRequestPerson? RequestedBy { get; set; }

    [BsonElement("assigned_to")]
    public AssetServiceRequestPerson? AssignedTo { get; set; }

    // Work Details.
    [BsonElement("estimated_downtime_hours")]
    public decimal? EstimatedDowntimeHours { get; set; }

    // Required Start / End Date & Time; shown as Planned Start / End.
    [BsonElement("planned_start")]
    public DateTime? PlannedStart { get; set; }

    [BsonElement("planned_end")]
    public DateTime? PlannedEnd { get; set; }

    // Set when the request first goes In Progress / is Closed, unless entered.
    [BsonElement("actual_start")]
    public DateTime? ActualStart { get; set; }

    [BsonElement("actual_end")]
    public DateTime? ActualEnd { get; set; }

    // Related Service Request on the form: another request's number.
    [BsonElement("related_service_request")]
    public string? RelatedServiceRequest { get; set; }

    // Additional Information.
    [BsonElement("tags")]
    public List<string> Tags { get; set; } = [];

    [BsonElement("reference_number")]
    public string? ReferenceNumber { get; set; }

    // Attachments tab.
    [BsonElement("attachments")]
    public List<AssetServiceRequestAttachment> Attachments { get; set; } = [];

    // Updates tab, oldest first.
    [BsonElement("updates")]
    public List<AssetServiceRequestUpdate> Updates { get; set; } = [];

    // Related tab. The first work order is the request's Work Order (Work Details).
    [BsonElement("work_orders")]
    public List<AssetServiceRequestWorkOrder> WorkOrders { get; set; } = [];

    [BsonElement("related_assets")]
    public List<AssetServiceRequestRelatedAsset> RelatedAssets { get; set; } = [];

    [BsonElement("spare_parts")]
    public List<AssetServiceRequestSparePart> SpareParts { get; set; } = [];

    // Asset documents linked with Link Document (the request's own attachments are listed too).
    [BsonElement("documents")]
    public List<AssetServiceRequestDocument> Documents { get; set; } = [];

    [BsonElement("submitted_at")]
    public DateTime? SubmittedAt { get; set; }
}

/// <summary>The asset, as it was when the request was raised.</summary>
public sealed class AssetServiceRequestAsset
{
    [BsonElement("asset_registration_id")]
    public string AssetRegistrationId { get; set; } = string.Empty;

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("asset_tag")]
    public string? AssetTag { get; set; }

    [BsonElement("category")]
    public string? Category { get; set; }

    [BsonElement("location")]
    public string? Location { get; set; }

    [BsonElement("department")]
    public string? Department { get; set; }

    [BsonElement("status")]
    public string? Status { get; set; }

    [BsonElement("image_url")]
    public string? ImageUrl { get; set; }
}

/// <summary>A location or department picked from a list.</summary>
public sealed class AssetServiceRequestReference
{
    [BsonElement("id")]
    public string? Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class AssetServiceRequestPerson
{
    [BsonElement("user_id")]
    public string? UserId { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    // e.g. "Technician".
    [BsonElement("role")]
    public string? Role { get; set; }
}

/// <summary>A file uploaded through the Media service; this keeps its link and details.</summary>
public sealed class AssetServiceRequestAttachment
{
    // Generated; used to delete one attachment.
    [BsonElement("attachment_id")]
    public string AttachmentId { get; set; } = string.Empty;

    [BsonElement("file_name")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("file_url")]
    public string FileUrl { get; set; } = string.Empty;

    // "Image", "PDF", "Document", "Spreadsheet" or "Video", from the file extension.
    [BsonElement("file_type")]
    public string? FileType { get; set; }

    [BsonElement("content_type")]
    public string? ContentType { get; set; }

    [BsonElement("file_size")]
    public long? FileSize { get; set; }

    [BsonElement("thumbnail_url")]
    public string? ThumbnailUrl { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("uploaded_by")]
    public string? UploadedBy { get; set; }

    [BsonElement("uploaded_at")]
    public DateTime? UploadedAt { get; set; }
}

/// <summary>One entry on the Updates timeline.</summary>
public sealed class AssetServiceRequestUpdate
{
    [BsonElement("update_id")]
    public string UpdateId { get; set; } = string.Empty;

    // "Created", "Comment", "Status Change" or "Work Order".
    [BsonElement("update_type")]
    public string UpdateType { get; set; } = string.Empty;

    // e.g. "Created the Service Request", "Added Comment", "Updated Status".
    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;

    [BsonElement("message")]
    public string? Message { get; set; }

    // The request's status after this update; shown as "Status: In Progress".
    [BsonElement("status")]
    public string? Status { get; set; }

    [BsonElement("recommended_action")]
    public string? RecommendedAction { get; set; }

    [BsonElement("attachments")]
    public List<AssetServiceRequestAttachment> Attachments { get; set; } = [];

    [BsonElement("user_id")]
    public string? UserId { get; set; }

    [BsonElement("user_name")]
    public string? UserName { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>A work order in the Maintenance service, as it was when last read.</summary>
public sealed class AssetServiceRequestWorkOrder
{
    // The work order number, e.g. "WO-2025-076".
    [BsonElement("work_order_id")]
    public string WorkOrderId { get; set; } = string.Empty;

    [BsonElement("title")]
    public string? Title { get; set; }

    [BsonElement("work_type")]
    public string? WorkType { get; set; }

    [BsonElement("status")]
    public string? Status { get; set; }

    [BsonElement("priority")]
    public string? Priority { get; set; }

    [BsonElement("planned_start")]
    public DateTime? PlannedStart { get; set; }

    [BsonElement("planned_end")]
    public DateTime? PlannedEnd { get; set; }

    [BsonElement("assigned_to")]
    public string? AssignedTo { get; set; }

    [BsonElement("linked_by")]
    public string? LinkedBy { get; set; }

    [BsonElement("linked_at")]
    public DateTime? LinkedAt { get; set; }
}

/// <summary>Another asset, or a component, related to the request.</summary>
public sealed class AssetServiceRequestRelatedAsset
{
    // "Asset" or "Component".
    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    // AssetRegistrationId, or the AssetComponent id.
    [BsonElement("id")]
    public string ReferenceId { get; set; } = string.Empty;

    // Asset tag or component tag, e.g. "GEN-00045-ALT-01".
    [BsonElement("number")]
    public string? Number { get; set; }

    [BsonElement("name")]
    public string? Name { get; set; }

    // e.g. "Part of GEN-00045".
    [BsonElement("relationship")]
    public string? Relationship { get; set; }
}

/// <summary>A spare part used for the request.</summary>
public sealed class AssetServiceRequestSparePart
{
    // The part number, e.g. "SP-0456".
    [BsonElement("part_id")]
    public string PartId { get; set; } = string.Empty;

    [BsonElement("part_name")]
    public string? PartName { get; set; }

    [BsonElement("quantity_used")]
    public int QuantityUsed { get; set; }

    [BsonElement("used_date")]
    public DateTime? UsedDate { get; set; }

    [BsonElement("work_order_id")]
    public string? WorkOrderId { get; set; }
}

/// <summary>An asset document (AssetDocumentRecord) linked to the request.</summary>
public sealed class AssetServiceRequestDocument
{
    [BsonElement("document_id")]
    public string DocumentId { get; set; } = string.Empty;

    [BsonElement("document_name")]
    public string? DocumentName { get; set; }

    [BsonElement("file_name")]
    public string? FileName { get; set; }

    [BsonElement("file_url")]
    public string? FileUrl { get; set; }

    [BsonElement("file_type")]
    public string? FileType { get; set; }

    [BsonElement("uploaded_by")]
    public string? UploadedBy { get; set; }

    [BsonElement("uploaded_at")]
    public DateTime? UploadedAt { get; set; }
}
