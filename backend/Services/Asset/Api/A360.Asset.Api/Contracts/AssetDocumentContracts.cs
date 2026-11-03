using A360.Asset.Domain.Entities;
using A360.Security;

namespace A360.Asset.Api.Contracts;

/// <summary>
/// Upload Document form. Upload the file with POST /api/media/upload (category "asset-documents") first,
/// then send its url and details here. One document is saved for each selected asset.
/// </summary>
public sealed record CreateAssetDocumentRequest(
    IReadOnlyCollection<string>? AssetRegistrationIds,
    string? DocumentName,
    string? DocumentType,
    string? LifecycleStage,
    string? Version,
    DateTime? ExpiryDate,
    string? Description,
    IReadOnlyCollection<string>? Tags,
    string? Status,
    string? FileName,
    string? FileUrl,
    string? ContentType,
    long? FileSize,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public AssetDocumentRecord ToEntity(AssetRegistration asset, CurrentUser? currentUser)
    {
        var document = new AssetDocumentRecord
        {
            AssetRegistrationId = asset.AssetRegistrationId,
            AssetName = asset.BasicInformation.AssetName,
            AssetTag = asset.BasicInformation.AssetTag,
            DocumentName = DocumentName!.Trim(),
            DocumentType = DocumentType!.Trim(),
            LifecycleStage = LifecycleStage!.Trim(),
            Version = Version!.Trim(),
            ExpiryDate = ContractText.DateOnly(ExpiryDate),
            Description = ContractText.TrimOrNull(Description),
            Tags = ContractText.CleanList(Tags, StringComparer.OrdinalIgnoreCase),
            Status = ContractText.TrimOrNull(Status) ?? AssetDocumentStatuses.Active,
            FileName = FileName!.Trim(),
            FileUrl = FileUrl!.Trim(),
            ContentType = ContractText.TrimOrNull(ContentType),
            FileSize = FileSize,
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };

        return document;
    }
}

/// <summary>Edit from the Document Details panel. Leave FileUrl empty to keep the current file.</summary>
public sealed record UpdateAssetDocumentRequest(
    string? AssetRegistrationId,
    string? DocumentName,
    string? DocumentType,
    string? LifecycleStage,
    string? Version,
    DateTime? ExpiryDate,
    string? Description,
    IReadOnlyCollection<string>? Tags,
    string? Status,
    string? FileName,
    string? FileUrl,
    string? ContentType,
    long? FileSize)
{
    public void ApplyTo(AssetDocumentRecord document, AssetRegistration asset, CurrentUser? currentUser)
    {
        document.AssetRegistrationId = asset.AssetRegistrationId;
        document.AssetName = asset.BasicInformation.AssetName;
        document.AssetTag = asset.BasicInformation.AssetTag;
        document.DocumentName = DocumentName!.Trim();
        document.DocumentType = DocumentType!.Trim();
        document.LifecycleStage = LifecycleStage!.Trim();
        document.Version = Version!.Trim();
        document.ExpiryDate = ContractText.DateOnly(ExpiryDate);
        document.Description = ContractText.TrimOrNull(Description);
        document.Tags = ContractText.CleanList(Tags, StringComparer.OrdinalIgnoreCase);
        document.Status = ContractText.TrimOrNull(Status) ?? AssetDocumentStatuses.Active;

        if (!string.IsNullOrWhiteSpace(FileUrl))
        {
            document.FileUrl = FileUrl.Trim();
            document.FileName = FileName!.Trim();
            document.ContentType = ContractText.TrimOrNull(ContentType);
            document.FileSize = FileSize;
        }

        document.UpdatedBy = currentUser?.UserName;
        document.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record DeleteAssetDocumentsRequest(IReadOnlyCollection<string>? Ids);

public static class AssetDocumentStatuses
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";

    public static IEnumerable<string> All => [Active, Inactive];
}

/// <summary>File types and size accepted by the Upload Document form.</summary>
public static class AssetDocumentFiles
{
    public const long MaxFileSizeBytes = 25 * 1024 * 1024;

    public static readonly string[] AllowedExtensions = [".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".xls", ".xlsx"];
}

/// <param name="UploadedBy">Who uploaded the document (CreatedBy).</param>
/// <param name="UploadDate">When it was uploaded (CreatedAt).</param>
public sealed record AssetDocumentResponse(
    string Id,
    string AssetRegistrationId,
    string? AssetName,
    string? AssetTag,
    string DocumentName,
    string DocumentType,
    string LifecycleStage,
    string Version,
    DateTime? ExpiryDate,
    string? Description,
    IReadOnlyCollection<string> Tags,
    string? Status,
    string FileName,
    string FileUrl,
    string? ContentType,
    long? FileSize,
    string? UploadedBy,
    DateTime? UploadDate,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static AssetDocumentResponse FromEntity(AssetDocumentRecord document)
    {
        return new AssetDocumentResponse(
            document.Id,
            document.AssetRegistrationId,
            document.AssetName,
            document.AssetTag,
            document.DocumentName,
            document.DocumentType,
            document.LifecycleStage,
            document.Version,
            document.ExpiryDate,
            document.Description,
            document.Tags,
            document.Status,
            document.FileName,
            document.FileUrl,
            document.ContentType,
            document.FileSize,
            document.CreatedBy,
            document.CreatedAt,
            document.UpdatedBy,
            document.UpdatedAt,
            document.ClientId,
            document.TenantId);
    }
}

public sealed record AssetDocumentListResponse(
    IReadOnlyCollection<AssetDocumentResponse> Items,
    long TotalCount,
    int Page,
    int PageSize);
