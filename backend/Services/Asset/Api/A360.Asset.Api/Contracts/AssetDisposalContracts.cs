using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;

namespace A360.Asset.Api.Contracts;

/// <summary>A disposal method card on step 3.</summary>
/// <param name="VendorRequired">The Vendor field is required for this method.</param>
public sealed record AssetDisposalMethodOption(string Method, string Description, bool VendorRequired);

public static class AssetDisposalOptions
{
    public const string DefaultCurrency = "AED";
    public const string NormalPriority = "Normal";
    public const string Mandatory = "Mandatory";

    // Set on each asset when its disposal is completed; the asset list counts it.
    public const string DisposedAssetStatus = "Disposed";

    public static readonly OptionList Reasons =
        new("End of Life", "Damaged Beyond Repair", "Obsolete", "Uneconomical to Repair", "Lost / Stolen", "Surplus / Not Required", "Other");

    public static readonly OptionList Priorities = new("Low", NormalPriority, "High", "Urgent");

    public static readonly IReadOnlyList<AssetDisposalMethodOption> Methods =
    [
        new("Recycle", "Send to certified recycler for environmentally responsible disposal.", true),
        new("Scrap", "Dispose as scrap (material recovery).", false),
        new("Return to Vendor", "Return to original vendor or supplier.", true),
        new("Donate", "Donate to approved organization.", true),
        new("Sell / Auction", "Sell or auction to a buyer.", true)
    ];

    public static readonly OptionList MethodNames = new(Methods.Select(method => method.Method).ToArray());

    public static readonly OptionList ApprovalTypes = new(Mandatory, "Optional");

    // Suggestions for the Approval Level column; other names can be typed.
    public static readonly IReadOnlyList<string> ApprovalLevels = ["Line Manager", "Department Head", "Finance Approval", "Final Approval"];

    public static readonly OptionList Statuses = new(
        AssetDisposalRequestRepository.DraftStatus,
        AssetDisposalRequestRepository.PendingApprovalStatus,
        AssetDisposalRequestRepository.ApprovedStatus,
        AssetDisposalRequestRepository.RejectedStatus,
        AssetDisposalRequestRepository.CompletedStatus,
        AssetDisposalRequestRepository.CancelledStatus);

    public static bool VendorRequired(string? method) =>
        Methods.FirstOrDefault(option => option.Method == method)?.VendorRequired ?? false;
}

// ---- Requests ----

/// <summary>Step 1: Request Details.</summary>
/// <param name="Currency">The currency of the estimated values; AED when empty.</param>
public sealed record AssetDisposalDetailsRequest(
    string? RequestTitle,
    DateTime? RequestDate,
    AssetDisposalPersonRequest? RequestedBy,
    string? Reason,
    string? DisposalCategory,
    string? Priority,
    string? Justification,
    string? Currency,
    string? CreatedBy,
    string? ClientId,
    string? TenantId);

public sealed record AssetDisposalPersonRequest(string? UserId, string? Name, string? RoleDepartment)
{
    /// <summary>Null when nothing was picked.</summary>
    public AssetDisposalPerson? ToEntity() =>
        string.IsNullOrWhiteSpace(UserId) && string.IsNullOrWhiteSpace(Name)
            ? null
            : new AssetDisposalPerson
            {
                UserId = ContractText.TrimOrNull(UserId),
                Name = Name?.Trim() ?? string.Empty,
                RoleDepartment = ContractText.TrimOrNull(RoleDepartment)
            };
}

/// <summary>
/// Step 2: the selected assets with their estimated values (also sent again when Est. Value is edited on step 3).
/// The list replaces the saved one; names, tags and conditions are loaded on the server.
/// </summary>
public sealed record AssetDisposalAssetsRequest(IReadOnlyCollection<AssetDisposalAssetRequest>? Assets);

public sealed record AssetDisposalAssetRequest(string? AssetRegistrationId, decimal? EstimatedValue);

/// <summary>Add by Tag on step 2: asset tags or serial numbers to add to the selection.</summary>
public sealed record AddAssetDisposalAssetsByTagRequest(IReadOnlyCollection<string>? Tags);

/// <summary>Step 3: Disposal Method.</summary>
/// <param name="Method">One of AssetDisposalOptions.Methods.</param>
/// <param name="Vendor">The recycler, vendor, buyer or receiving organisation; required except for Scrap.</param>
public sealed record AssetDisposalMethodRequest(
    string? Method,
    DateTime? ExpectedDisposalDate,
    AssetDisposalVendorRequest? Vendor,
    string? ContactPerson,
    string? ContactNumber,
    string? Instructions)
{
    public AssetDisposalMethod ToEntity() => new()
    {
        Method = AssetDisposalOptions.MethodNames.NormalizeOrKeep(Method),
        ExpectedDisposalDate = ContractText.DateOnly(ExpectedDisposalDate),
        Vendor = Vendor?.ToEntity(),
        ContactPerson = ContractText.TrimOrNull(ContactPerson),
        ContactNumber = ContractText.TrimOrNull(ContactNumber),
        Instructions = ContractText.TrimOrNull(Instructions)
    };
}

/// <param name="VendorId">The supplier picked from the list, if any.</param>
public sealed record AssetDisposalVendorRequest(string? VendorId, string? Name, string? Address, string? TradeLicense)
{
    /// <summary>Null when nothing was picked.</summary>
    public AssetDisposalVendor? ToEntity() =>
        string.IsNullOrWhiteSpace(VendorId) && string.IsNullOrWhiteSpace(Name)
            ? null
            : new AssetDisposalVendor
            {
                VendorId = ContractText.TrimOrNull(VendorId),
                Name = Name?.Trim() ?? string.Empty,
                Address = ContractText.TrimOrNull(Address),
                TradeLicense = ContractText.TrimOrNull(TradeLicense)
            };
}

/// <summary>Step 4: Approval Workflow, Attachments and Additional Remarks.</summary>
/// <param name="Approvers">In approval order (the drag handles); levels are numbered from 1.</param>
/// <param name="NotifyApprovers">"Send approval notifications automatically".</param>
public sealed record AssetDisposalApprovalRequest(
    IReadOnlyCollection<AssetDisposalApproverRequest>? Approvers,
    bool NotifyApprovers,
    IReadOnlyCollection<AssetDisposalAttachmentRequest>? Attachments,
    string? Remarks);

/// <param name="ApprovalLevel">e.g. "Line Manager"; see AssetDisposalOptions.ApprovalLevels.</param>
/// <param name="ApprovalType">"Mandatory" (default) or "Optional".</param>
public sealed record AssetDisposalApproverRequest(
    string? ApprovalLevel,
    AssetDisposalPersonRequest? Approver,
    string? ApprovalType);

/// <summary>A file already uploaded through the Media service (PDF, DOC, XLS, JPG, PNG or ZIP, at most 10 MB).</summary>
public sealed record AssetDisposalAttachmentRequest(
    string? FileName,
    string? FileUrl,
    string? FileType,
    long? FileSize,
    string? Description,
    string? UploadedBy,
    DateTime? UploadedAt)
{
    public AssetDisposalAttachment ToEntity(string? currentUser) => new()
    {
        FileName = FileName?.Trim() ?? string.Empty,
        FileUrl = FileUrl?.Trim() ?? string.Empty,
        FileType = ContractText.TrimOrNull(FileType)?.ToUpperInvariant()
            ?? ContractText.TrimOrNull(Path.GetExtension(FileName ?? string.Empty).TrimStart('.'))?.ToUpperInvariant(),
        FileSize = FileSize,
        Description = ContractText.TrimOrNull(Description),
        UploadedBy = ContractText.FirstNonEmpty(UploadedBy, currentUser),
        UploadedAt = UploadedAt ?? DateTime.UtcNow
    };
}

/// <summary>Approve, reject or skip the current approval level. Comments are required to reject.</summary>
public sealed record AssetDisposalDecisionRequest(string? Comments);

/// <summary>Marks an approved request as carried out; its assets become Disposed.</summary>
/// <param name="RecoveredValue">Money received (sale, scrap); 0 or empty otherwise.</param>
/// <param name="Certificate">Certificate of recycling / destruction, uploaded through the Media service.</param>
public sealed record CompleteAssetDisposalRequest(
    DateTime? ActualDisposalDate,
    decimal? RecoveredValue,
    string? CertificateNumber,
    AssetDisposalAttachmentRequest? Certificate,
    string? Remarks);

public sealed record CancelAssetDisposalRequest(string? Reason);

// ---- Responses ----

public sealed record AssetDisposalOptionsResponse(
    IEnumerable<string> Reasons,
    IEnumerable<string> Priorities,
    IEnumerable<AssetDisposalMethodOption> Methods,
    IEnumerable<string> ApprovalTypes,
    IEnumerable<string> ApprovalLevels,
    IEnumerable<string> Statuses,
    string DefaultCurrency);

/// <summary>The Request Summary / Selection Summary panels.</summary>
public sealed record AssetDisposalSummary(
    string RequestTitle,
    DateTime? RequestDate,
    string? RequestedBy,
    string? Reason,
    string? DisposalCategory,
    string? Priority,
    int TotalAssets,
    decimal EstimatedValue,
    string Currency,
    string? DisposalMethod,
    string? Vendor,
    DateTime? ExpectedDate,
    string? Status);

/// <param name="CurrentApprover">The first level still pending, while the request awaits approval.</param>
public sealed record AssetDisposalRequestResponse(
    string Id,
    string RequestNumber,
    string? Status,
    int CurrentStep,
    string RequestTitle,
    DateTime? RequestDate,
    AssetDisposalPerson? RequestedBy,
    string? Reason,
    string? DisposalCategory,
    string? Priority,
    string? Justification,
    string Currency,
    int AssetCount,
    decimal TotalEstimatedValue,
    IReadOnlyCollection<AssetDisposalAsset> Assets,
    AssetDisposalMethod Method,
    AssetDisposalMethodOption? MethodDetails,
    IReadOnlyCollection<AssetDisposalApprover> Approvers,
    AssetDisposalApprover? CurrentApprover,
    bool NotifyApprovers,
    IReadOnlyCollection<AssetDisposalAttachment> Attachments,
    string? Remarks,
    AssetDisposalSummary Summary,
    string? SubmittedBy,
    DateTime? SubmittedAt,
    AssetDisposalCompletion? Completion,
    string? CancelledReason,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static AssetDisposalRequestResponse FromEntity(AssetDisposalRequest request)
    {
        var total = request.Assets.Sum(asset => asset.EstimatedValue);
        var currentApprover = CurrentApproverOf(request);

        return new AssetDisposalRequestResponse(
            request.Id,
            request.RequestNumber,
            request.Status,
            request.CurrentStep,
            request.RequestTitle,
            request.RequestDate,
            request.RequestedBy,
            request.Reason,
            request.DisposalCategory,
            request.Priority,
            request.Justification,
            request.Currency,
            request.Assets.Count,
            total,
            request.Assets,
            request.Method,
            AssetDisposalOptions.Methods.FirstOrDefault(option => option.Method == request.Method.Method),
            request.Approvers,
            currentApprover,
            request.NotifyApprovers,
            request.Attachments,
            request.Remarks,
            new AssetDisposalSummary(
                request.RequestTitle,
                request.RequestDate,
                request.RequestedBy?.Name,
                request.Reason,
                request.DisposalCategory,
                request.Priority,
                request.Assets.Count,
                total,
                request.Currency,
                request.Method.Method,
                request.Method.Vendor?.Name,
                request.Method.ExpectedDisposalDate,
                request.Status),
            request.SubmittedBy,
            request.SubmittedAt,
            request.Completion,
            request.CancelledReason,
            request.CreatedBy,
            request.CreatedAt,
            request.UpdatedBy,
            request.UpdatedAt,
            request.ClientId,
            request.TenantId);
    }

    /// <summary>The level now waiting for a decision: the first pending one, while the request awaits approval.</summary>
    public static AssetDisposalApprover? CurrentApproverOf(AssetDisposalRequest request) =>
        request.Status == AssetDisposalRequestRepository.PendingApprovalStatus
            ? request.Approvers.OrderBy(approver => approver.Level).FirstOrDefault(approver => approver.Status == AssetDisposalRequestRepository.PendingApproval)
            : null;
}

/// <summary>One row of the disposal request list.</summary>
/// <param name="PendingWith">The current approver's name and level, while awaiting approval.</param>
public sealed record AssetDisposalRequestListItemResponse(
    string Id,
    string RequestNumber,
    string RequestTitle,
    DateTime? RequestDate,
    string? RequestedBy,
    string? Reason,
    string? Priority,
    string? DisposalMethod,
    string? Vendor,
    DateTime? ExpectedDisposalDate,
    int AssetCount,
    decimal TotalEstimatedValue,
    string Currency,
    string? Status,
    string? PendingWith)
{
    public static AssetDisposalRequestListItemResponse FromEntity(AssetDisposalRequest request)
    {
        var current = AssetDisposalRequestResponse.CurrentApproverOf(request);
        return new AssetDisposalRequestListItemResponse(
            request.Id,
            request.RequestNumber,
            request.RequestTitle,
            request.RequestDate,
            request.RequestedBy?.Name,
            request.Reason,
            request.Priority,
            request.Method.Method,
            request.Method.Vendor?.Name,
            request.Method.ExpectedDisposalDate,
            request.Assets.Count,
            request.Assets.Sum(asset => asset.EstimatedValue),
            request.Currency,
            request.Status,
            current is null ? null : $"{current.Approver.Name} ({current.ApprovalLevel})");
    }
}

/// <param name="StatusCounts">Counts per status for the same filters except Status.</param>
public sealed record AssetDisposalRequestListResponse(
    IReadOnlyCollection<AssetDisposalRequestListItemResponse> Items,
    long TotalCount,
    IReadOnlyDictionary<string, long> StatusCounts,
    int Page,
    int PageSize);

/// <summary>Add by Tag / Import from Excel result.</summary>
/// <param name="NotFound">Tags that match no asset.</param>
/// <param name="Rejected">Tags whose asset cannot be added, with the reason (e.g. already disposed).</param>
public sealed record AssetDisposalAddAssetsResponse(
    AssetDisposalRequestResponse Request,
    int AddedCount,
    IReadOnlyCollection<string> NotFound,
    IReadOnlyDictionary<string, string> Rejected);
