using A360.Asset.Domain.Entities;

namespace A360.Asset.Api.Contracts;

/// <summary>Every image and document filed against one asset.</summary>
/// <param name="Images">From the Asset Images screen (AssetImage collection).</param>
/// <param name="Documents">From the Asset Documents screen (AssetDocument collection).</param>
/// <param name="RegistrationAttachments">Files uploaded while registering the asset (Purchase &amp; Warranty documents and Additional Information attachments).</param>
public sealed record AssetFilesResponse(
    string AssetRegistrationId,
    string? AssetName,
    string? AssetTag,
    IReadOnlyCollection<AssetImageResponse> Images,
    IReadOnlyCollection<AssetDocumentResponse> Documents,
    IReadOnlyCollection<AssetRegistrationAttachmentResponse> RegistrationAttachments,
    int ImageCount,
    int DocumentCount,
    int RegistrationAttachmentCount);

/// <param name="Source">"PurchaseWarranty" or "AdditionalInformation": the registration step the file was uploaded in.</param>
public sealed record AssetRegistrationAttachmentResponse(
    string Source,
    string FileName,
    string Url,
    string? ContentType,
    long? FileSize,
    DateTime? UploadedAt)
{
    public const string PurchaseWarrantySource = "PurchaseWarranty";
    public const string AdditionalInformationSource = "AdditionalInformation";

    public static AssetRegistrationAttachmentResponse FromEntity(AssetDocument document, string source)
    {
        return new AssetRegistrationAttachmentResponse(
            source,
            document.FileName,
            document.Url,
            document.ContentType,
            document.FileSize,
            document.UploadedAt);
    }
}
