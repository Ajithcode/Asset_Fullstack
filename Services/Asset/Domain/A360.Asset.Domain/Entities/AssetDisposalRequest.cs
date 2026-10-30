using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>A request to dispose of end-of-life or unwanted assets, from the New Disposal Request wizard.</summary>
/// <remarks>
/// Holds all five steps: request details, the selected assets, the disposal method, the approval workflow with
/// attachments and remarks, and the outcome. Status is BaseEntity.Status: Draft, Pending Approval, Approved,
/// Rejected, Completed or Cancelled. Asset and person names are copied when saved.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class AssetDisposalRequest : BaseEntity
{
    // e.g. "DSP-000012".
    [BsonElement("request_number")]
    public string RequestNumber { get; set; } = string.Empty;

    // Furthest step saved so far, 1 to 5, so the wizard can resume there.
    [BsonElement("current_step")]
    public int CurrentStep { get; set; }

    // Step 1: Request Details.
    [BsonElement("request_title")]
    public string RequestTitle { get; set; } = string.Empty;

    [BsonElement("request_date")]
    public DateTime? RequestDate { get; set; }

    [BsonElement("requested_by")]
    public AssetDisposalPerson? RequestedBy { get; set; }

    // e.g. "End of Life", "Damaged Beyond Repair".
    [BsonElement("reason")]
    public string? Reason { get; set; }

    // e.g. "IT Equipment".
    [BsonElement("disposal_category")]
    public string? DisposalCategory { get; set; }

    // "Low", "Normal", "High" or "Urgent".
    [BsonElement("priority")]
    public string? Priority { get; set; }

    [BsonElement("justification")]
    public string? Justification { get; set; }

    // Step 2: Select Assets.
    [BsonElement("assets")]
    public List<AssetDisposalAsset> Assets { get; set; } = [];

    // e.g. "AED"; the currency of the estimated values.
    [BsonElement("currency")]
    public string Currency { get; set; } = string.Empty;

    // Step 3: Disposal Method.
    [BsonElement("method")]
    public AssetDisposalMethod Method { get; set; } = new();

    // Step 4: Approval Workflow, Attachments and Additional Remarks.
    [BsonElement("approvers")]
    public List<AssetDisposalApprover> Approvers { get; set; } = [];

    // "Send approval notifications automatically".
    [BsonElement("notify_approvers")]
    public bool NotifyApprovers { get; set; } = true;

    [BsonElement("attachments")]
    public List<AssetDisposalAttachment> Attachments { get; set; } = [];

    [BsonElement("remarks")]
    public string? Remarks { get; set; }

    // Step 5 and after.
    [BsonElement("submitted_by")]
    public string? SubmittedBy { get; set; }

    [BsonElement("submitted_at")]
    public DateTime? SubmittedAt { get; set; }

    [BsonElement("completion")]
    public AssetDisposalCompletion? Completion { get; set; }

    [BsonElement("cancelled_reason")]
    public string? CancelledReason { get; set; }
}

/// <summary>A user picked from a list, e.g. Requested By or an approver.</summary>
public sealed class AssetDisposalPerson
{
    [BsonElement("user_id")]
    public string? UserId { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    // e.g. "IT Manager" or "Head - IT Department".
    [BsonElement("role_department")]
    public string? RoleDepartment { get; set; }
}

/// <summary>An asset in the request, as it was when picked, with its estimated disposal value.</summary>
public sealed class AssetDisposalAsset
{
    [BsonElement("asset_registration_id")]
    public string AssetRegistrationId { get; set; } = string.Empty;

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("asset_tag")]
    public string? AssetTag { get; set; }

    [BsonElement("category")]
    public string? Category { get; set; }

    [BsonElement("model")]
    public string? Model { get; set; }

    [BsonElement("serial_number")]
    public string? SerialNumber { get; set; }

    [BsonElement("manufacturer")]
    public string? Manufacturer { get; set; }

    [BsonElement("location")]
    public string? Location { get; set; }

    [BsonElement("department")]
    public string? Department { get; set; }

    // e.g. "End of Life", "Under Repair".
    [BsonElement("condition")]
    public string? Condition { get; set; }

    // The asset's status when picked, e.g. "Active"; it becomes "Disposed" when the request is completed.
    [BsonElement("status")]
    public string? Status { get; set; }

    [BsonElement("image_url")]
    public string? ImageUrl { get; set; }

    [BsonElement("estimated_value")]
    public decimal EstimatedValue { get; set; }
}

public sealed class AssetDisposalMethod
{
    // "Recycle", "Scrap", "Return to Vendor", "Donate" or "Sell / Auction".
    [BsonElement("method")]
    public string? Method { get; set; }

    [BsonElement("expected_disposal_date")]
    public DateTime? ExpectedDisposalDate { get; set; }

    // The recycler, scrap dealer, vendor, buyer or receiving organisation.
    [BsonElement("vendor")]
    public AssetDisposalVendor? Vendor { get; set; }

    [BsonElement("contact_person")]
    public string? ContactPerson { get; set; }

    [BsonElement("contact_number")]
    public string? ContactNumber { get; set; }

    [BsonElement("instructions")]
    public string? Instructions { get; set; }
}

public sealed class AssetDisposalVendor
{
    // The supplier record, when picked from the list.
    [BsonElement("vendor_id")]
    public string? VendorId { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    // e.g. "P.O. Box 12345, Dubai, UAE".
    [BsonElement("address")]
    public string? Address { get; set; }

    // e.g. "TRN-123456789".
    [BsonElement("trade_license")]
    public string? TradeLicense { get; set; }
}

/// <summary>One level of the approval workflow; levels are approved in order.</summary>
public sealed class AssetDisposalApprover
{
    // 1-based position in the sequence.
    [BsonElement("level")]
    public int Level { get; set; }

    // e.g. "Line Manager", "Department Head", "Finance Approval", "Final Approval".
    [BsonElement("approval_level")]
    public string ApprovalLevel { get; set; } = string.Empty;

    [BsonElement("approver")]
    public AssetDisposalPerson Approver { get; set; } = new();

    // "Mandatory" or "Optional"; an optional level does not hold up approval.
    [BsonElement("approval_type")]
    public string ApprovalType { get; set; } = string.Empty;

    // "Pending", "Approved", "Rejected" or "Skipped".
    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    [BsonElement("comments")]
    public string? Comments { get; set; }

    [BsonElement("acted_by")]
    public string? ActedBy { get; set; }

    [BsonElement("acted_at")]
    public DateTime? ActedAt { get; set; }
}

/// <summary>A file uploaded through the Media service; this keeps its link and details.</summary>
public sealed class AssetDisposalAttachment
{
    [BsonElement("file_name")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("file_url")]
    public string FileUrl { get; set; } = string.Empty;

    // e.g. "PDF", "ZIP".
    [BsonElement("file_type")]
    public string? FileType { get; set; }

    [BsonElement("file_size")]
    public long? FileSize { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("uploaded_by")]
    public string? UploadedBy { get; set; }

    [BsonElement("uploaded_at")]
    public DateTime? UploadedAt { get; set; }
}

/// <summary>Recorded when the assets have actually been disposed of.</summary>
public sealed class AssetDisposalCompletion
{
    [BsonElement("actual_disposal_date")]
    public DateTime? ActualDisposalDate { get; set; }

    // Money received, e.g. from a sale or scrap; 0 for recycling or donation.
    [BsonElement("recovered_value")]
    public decimal? RecoveredValue { get; set; }

    // e.g. the recycling or destruction certificate number.
    [BsonElement("certificate_number")]
    public string? CertificateNumber { get; set; }

    [BsonElement("certificate")]
    public AssetDisposalAttachment? Certificate { get; set; }

    [BsonElement("remarks")]
    public string? Remarks { get; set; }

    [BsonElement("completed_by")]
    public string? CompletedBy { get; set; }

    [BsonElement("completed_at")]
    public DateTime? CompletedAt { get; set; }
}
