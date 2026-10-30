using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;

namespace A360.Asset.Api.Contracts;

public static class AssetServiceRequestOptions
{
    public static readonly OptionList RequestTypes =
        new("Corrective", "Preventive", "Inspection", "Breakdown", "Calibration", "Installation", "Other");

    public static readonly OptionList Priorities = new("Low", "Medium", "High", "Critical");

    // Statuses a request can be moved to with an update; Draft is left by submitting.
    public static readonly OptionList UpdateStatuses = new(
        AssetServiceRequestRepository.OpenStatus,
        AssetServiceRequestRepository.InProgressStatus,
        AssetServiceRequestRepository.OnHoldStatus,
        AssetServiceRequestRepository.ApprovedStatus,
        AssetServiceRequestRepository.ClosedStatus,
        AssetServiceRequestRepository.CancelledStatus);

    public static readonly OptionList Statuses = new([AssetServiceRequestRepository.DraftStatus, .. UpdateStatuses.All]);

    public const string CreatedUpdate = "Created";
    public const string CommentUpdate = "Comment";
    public const string StatusChangeUpdate = "Status Change";
    public const string WorkOrderUpdate = "Work Order";

    // The "All Updates" filter.
    public static readonly OptionList UpdateTypes = new(CreatedUpdate, CommentUpdate, StatusChangeUpdate, WorkOrderUpdate);

    public const string AssetRelation = "Asset";
    public const string ComponentRelation = "Component";

    public static readonly OptionList RelatedAssetTypes = new(AssetRelation, ComponentRelation);

    // The File Types filter, worked out from the file extension.
    private static readonly Dictionary<string, string> FileTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "Image",
        [".jpeg"] = "Image",
        [".png"] = "Image",
        [".gif"] = "Image",
        [".webp"] = "Image",
        [".pdf"] = "PDF",
        [".doc"] = "Document",
        [".docx"] = "Document",
        [".xls"] = "Spreadsheet",
        [".xlsx"] = "Spreadsheet",
        [".csv"] = "Spreadsheet",
        [".mp4"] = "Video"
    };

    public static IEnumerable<string> FileTypes => FileTypesByExtension.Values.Distinct();

    public static IEnumerable<string> FileExtensions => FileTypesByExtension.Keys;

    /// <summary>"Image", "PDF", "Document", "Spreadsheet" or "Video"; null for other files.</summary>
    public static string? FileTypeOf(string? fileName) =>
        FileTypesByExtension.GetValueOrDefault(Path.GetExtension(fileName ?? string.Empty));
}

// ---- Requests ----

/// <summary>
/// The New Service Request form (and Edit). Submit Service Request creates it as Open; Save as Draft
/// (?saveAsDraft=true) keeps it as a Draft and skips the required fields except the asset and title.
/// </summary>
/// <param name="AssetRegistrationId">The asset the request is for; its name, tag and location are copied.</param>
/// <param name="RequestedDate">Defaults to now.</param>
/// <param name="Location">Defaults to the asset's location.</param>
/// <param name="Department">Defaults to the asset's custodian department.</param>
/// <param name="RequestedBy">Defaults to the signed-in user.</param>
/// <param name="PlannedStart">Required Start Date &amp; Time.</param>
/// <param name="PlannedEnd">Required End Date &amp; Time.</param>
/// <param name="RelatedWorkOrderId">A work order number to link, e.g. "WO-2025-076".</param>
/// <param name="RelatedServiceRequest">Another request's number, e.g. "SR-2025-015".</param>
/// <param name="Attachments">Files already uploaded through the Media service. On edit, leave empty (null) to keep the current ones.</param>
public sealed record SaveAssetServiceRequestRequest(
    string? AssetRegistrationId,
    string? Title,
    string? RequestType,
    string? Priority,
    DateTime? RequestedDate,
    DateTime? TargetDate,
    string? Description,
    AssetServiceRequestReferenceRequest? Location,
    AssetServiceRequestReferenceRequest? Department,
    AssetServiceRequestPersonRequest? RequestedBy,
    AssetServiceRequestPersonRequest? AssignedTo,
    decimal? EstimatedDowntimeHours,
    DateTime? PlannedStart,
    DateTime? PlannedEnd,
    DateTime? ActualStart,
    DateTime? ActualEnd,
    string? RelatedWorkOrderId,
    string? RelatedServiceRequest,
    IReadOnlyCollection<AssetServiceRequestAttachmentRequest>? Attachments,
    IReadOnlyCollection<string>? Tags,
    string? ReferenceNumber,
    string? CreatedBy,
    string? ClientId,
    string? TenantId);

public sealed record AssetServiceRequestReferenceRequest(string? Id, string? Name)
{
    /// <summary>Null when nothing was picked.</summary>
    public AssetServiceRequestReference? ToEntity() =>
        string.IsNullOrWhiteSpace(Id) && string.IsNullOrWhiteSpace(Name)
            ? null
            : new AssetServiceRequestReference { Id = ContractText.TrimOrNull(Id), Name = Name?.Trim() ?? string.Empty };
}

/// <param name="Role">e.g. "Technician".</param>
public sealed record AssetServiceRequestPersonRequest(string? UserId, string? Name, string? Role)
{
    /// <summary>Null when nothing was picked.</summary>
    public AssetServiceRequestPerson? ToEntity() =>
        string.IsNullOrWhiteSpace(UserId) && string.IsNullOrWhiteSpace(Name)
            ? null
            : new AssetServiceRequestPerson
            {
                UserId = ContractText.TrimOrNull(UserId),
                Name = Name?.Trim() ?? string.Empty,
                Role = ContractText.TrimOrNull(Role)
            };
}

/// <summary>A file already uploaded through the Media service (PDF, JPG, PNG, DOC, XLS or MP4, at most 50 MB).</summary>
public sealed record AssetServiceRequestAttachmentRequest(
    string? FileName,
    string? FileUrl,
    string? ContentType,
    long? FileSize,
    string? ThumbnailUrl,
    string? Description)
{
    public AssetServiceRequestAttachment ToEntity(string? uploadedBy) => new()
    {
        AttachmentId = Guid.NewGuid().ToString("N"),
        FileName = FileName?.Trim() ?? string.Empty,
        FileUrl = FileUrl?.Trim() ?? string.Empty,
        FileType = AssetServiceRequestOptions.FileTypeOf(FileName),
        ContentType = ContractText.TrimOrNull(ContentType),
        FileSize = FileSize,
        ThumbnailUrl = ContractText.TrimOrNull(ThumbnailUrl),
        Description = ContractText.TrimOrNull(Description),
        UploadedBy = uploadedBy,
        UploadedAt = DateTime.UtcNow
    };
}

/// <summary>
/// An entry on the Updates tab: a comment, a status change (when Status is sent), or both.
/// </summary>
/// <param name="Status">The new status, e.g. "In Progress"; leave empty for a comment only.</param>
/// <param name="RecommendedAction">e.g. "Replace alternator bearing (Part No. SP-0456)".</param>
/// <param name="Attachments">Photos or files for this update, already uploaded through the Media service.</param>
public sealed record AddAssetServiceRequestUpdateRequest(
    string? Message,
    string? Status,
    string? RecommendedAction,
    IReadOnlyCollection<AssetServiceRequestAttachmentRequest>? Attachments);

/// <summary>Add Attachment / Upload Attachment on the Attachments tab.</summary>
public sealed record AddAssetServiceRequestAttachmentsRequest(IReadOnlyCollection<AssetServiceRequestAttachmentRequest>? Attachments);

/// <summary>Deletes the ticked attachments.</summary>
public sealed record DeleteAssetServiceRequestAttachmentsRequest(IReadOnlyCollection<string>? AttachmentIds);

/// <param name="WorkOrderId">The work order number, e.g. "WO-2025-076".</param>
public sealed record LinkAssetServiceRequestWorkOrderRequest(string? WorkOrderId);

/// <summary>
/// Convert to Work Order: after the work order is created in the Maintenance service (POST /api/work-orders),
/// this links it as the request's work order and records it on the Updates tab.
/// </summary>
/// <param name="Message">Defaults to "Work order {id} created and linked to this service request."</param>
/// <param name="CloseRequest">Close the request now that the work continues on the work order (default true).</param>
public sealed record ConvertAssetServiceRequestRequest(string? WorkOrderId, string? Message, bool CloseRequest = true);

/// <param name="Type">"Asset" or "Component".</param>
/// <param name="ReferenceId">The AssetRegistrationId, or the AssetComponent id.</param>
/// <param name="Relationship">Defaults to e.g. "Part of GEN-00045" for a component of another asset.</param>
public sealed record LinkAssetServiceRequestRelatedAssetRequest(string? Type, string? ReferenceId, string? Relationship);

/// <param name="PartId">The part number, e.g. "SP-0456".</param>
/// <param name="WorkOrderId">The work order the part was used on, if any.</param>
public sealed record LinkAssetServiceRequestSparePartRequest(string? PartId, int QuantityUsed, DateTime? UsedDate, string? WorkOrderId);

/// <param name="DocumentId">An asset document's id (from the asset's Documents).</param>
public sealed record LinkAssetServiceRequestDocumentRequest(string? DocumentId);

// ---- Responses ----

public sealed record AssetServiceRequestOptionsResponse(
    IEnumerable<string> RequestTypes,
    IEnumerable<string> Priorities,
    IEnumerable<string> Statuses,
    IEnumerable<string> UpdateTypes,
    IEnumerable<string> FileTypes,
    IEnumerable<string> FileExtensions,
    IEnumerable<string> RelatedAssetTypes);

/// <summary>The tab counts and the Related Information panel.</summary>
/// <param name="RelatedCount">Work orders + related assets + spare parts + documents (the Related tab).</param>
/// <param name="SparePartsCount">Total quantity of parts used ("2 Parts").</param>
/// <param name="DocumentsCount">Attachments plus linked asset documents.</param>
public sealed record AssetServiceRequestCounts(
    int UpdatesCount,
    int AttachmentsCount,
    int RelatedCount,
    int WorkOrdersCount,
    int SparePartsCount,
    int DocumentsCount)
{
    public static AssetServiceRequestCounts FromEntity(AssetServiceRequest request)
    {
        var documents = request.Attachments.Count + request.Documents.Count;
        return new AssetServiceRequestCounts(
            request.Updates.Count,
            request.Attachments.Count,
            request.WorkOrders.Count + request.RelatedAssets.Count + request.SpareParts.Count + documents,
            request.WorkOrders.Count,
            request.SpareParts.Sum(part => part.QuantityUsed),
            documents);
    }
}

/// <param name="WorkOrder">The request's work order (the first linked), for Work Details.</param>
public sealed record AssetServiceRequestResponse(
    string Id,
    string RequestNumber,
    string? Status,
    AssetServiceRequestAsset Asset,
    string Title,
    string? RequestType,
    string? Priority,
    DateTime? RequestedDate,
    DateTime? TargetDate,
    string? Description,
    AssetServiceRequestReference? Location,
    AssetServiceRequestReference? Department,
    AssetServiceRequestPerson? RequestedBy,
    AssetServiceRequestPerson? AssignedTo,
    decimal? EstimatedDowntimeHours,
    DateTime? PlannedStart,
    DateTime? PlannedEnd,
    DateTime? ActualStart,
    DateTime? ActualEnd,
    AssetServiceRequestWorkOrder? WorkOrder,
    string? RelatedServiceRequest,
    IReadOnlyCollection<string> Tags,
    string? ReferenceNumber,
    IReadOnlyCollection<AssetServiceRequestAttachment> Attachments,
    AssetServiceRequestCounts Counts,
    DateTime? SubmittedAt,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static AssetServiceRequestResponse FromEntity(AssetServiceRequest request) => new(
        request.Id,
        request.RequestNumber,
        request.Status,
        request.Asset,
        request.Title,
        request.RequestType,
        request.Priority,
        request.RequestedDate,
        request.TargetDate,
        request.Description,
        request.Location,
        request.Department,
        request.RequestedBy,
        request.AssignedTo,
        request.EstimatedDowntimeHours,
        request.PlannedStart,
        request.PlannedEnd,
        request.ActualStart,
        request.ActualEnd,
        request.WorkOrders.FirstOrDefault(),
        request.RelatedServiceRequest,
        request.Tags,
        request.ReferenceNumber,
        request.Attachments,
        AssetServiceRequestCounts.FromEntity(request),
        request.SubmittedAt,
        request.CreatedBy,
        request.CreatedAt,
        request.UpdatedBy,
        request.UpdatedAt,
        request.ClientId,
        request.TenantId);
}

/// <summary>One row of a service request list, or of Recent Service Requests.</summary>
public sealed record AssetServiceRequestListItemResponse(
    string Id,
    string RequestNumber,
    string Title,
    string? Status,
    string AssetRegistrationId,
    string? AssetTag,
    string? AssetName,
    string? RequestType,
    string? Priority,
    DateTime? RequestedDate,
    DateTime? TargetDate,
    string? Location,
    string? RequestedBy,
    string? AssignedTo,
    string? WorkOrderId,
    int AttachmentsCount)
{
    public static AssetServiceRequestListItemResponse FromEntity(AssetServiceRequest request) => new(
        request.Id,
        request.RequestNumber,
        request.Title,
        request.Status,
        request.Asset.AssetRegistrationId,
        request.Asset.AssetTag,
        request.Asset.AssetName,
        request.RequestType,
        request.Priority,
        request.RequestedDate,
        request.TargetDate,
        request.Location?.Name,
        request.RequestedBy?.Name,
        request.AssignedTo?.Name,
        request.WorkOrders.FirstOrDefault()?.WorkOrderId,
        request.Attachments.Count);
}

/// <param name="StatusCounts">Counts per status for the same filters except Status.</param>
public sealed record AssetServiceRequestListResponse(
    IReadOnlyCollection<AssetServiceRequestListItemResponse> Items,
    long TotalCount,
    IReadOnlyDictionary<string, long> StatusCounts,
    int Page,
    int PageSize);

/// <summary>The Updates tab, newest first.</summary>
public sealed record AssetServiceRequestUpdatesResponse(
    IReadOnlyCollection<AssetServiceRequestUpdate> Items,
    int TotalCount);

/// <summary>The Attachments tab.</summary>
public sealed record AssetServiceRequestAttachmentsResponse(
    IReadOnlyCollection<AssetServiceRequestAttachment> Items,
    int TotalCount,
    IReadOnlyCollection<string> UploadedBy,
    int Page,
    int PageSize);

/// <summary>A row of Related Documents: an attachment ("Service Request") or a linked asset document ("Asset").</summary>
/// <param name="Id">The attachment id or the asset document id.</param>
public sealed record AssetServiceRequestDocumentResponse(
    string Id,
    string? DocumentName,
    string? FileType,
    string? FileUrl,
    DateTime? UploadedAt,
    string? UploadedBy,
    string RelatedTo);

/// <summary>The Related tab. Work orders show their current details from the Maintenance service.</summary>
public sealed record AssetServiceRequestRelatedResponse(
    IReadOnlyCollection<AssetServiceRequestWorkOrder> WorkOrders,
    IReadOnlyCollection<AssetServiceRequestRelatedAsset> RelatedAssets,
    IReadOnlyCollection<AssetServiceRequestSparePart> SpareParts,
    IReadOnlyCollection<AssetServiceRequestDocumentResponse> Documents,
    AssetServiceRequestCounts Counts);
