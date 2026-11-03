using A360.Asset.Domain.Entities;
using A360.Security;

namespace A360.Asset.Api.Contracts;

/// <summary>
/// Upload Images form. Upload each file with POST /api/media/upload (category "asset-images") first,
/// then send the urls and shared details here. Every image is saved once for each selected asset.
/// </summary>
/// <param name="Location">Leave empty to use each asset's own location.</param>
public sealed record CreateAssetImagesRequest(
    IReadOnlyCollection<string>? AssetRegistrationIds,
    IReadOnlyCollection<AssetImageFileRequest>? Images,
    string? ImageType,
    IReadOnlyCollection<string>? Tags,
    string? Location,
    string? Remarks,
    string? Description,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public AssetImage ToEntity(AssetRegistration asset, AssetImageFileRequest file, CurrentUser? currentUser)
    {
        var image = new AssetImage
        {
            AssetRegistrationId = asset.AssetRegistrationId,
            AssetName = asset.BasicInformation.AssetName,
            AssetTag = asset.BasicInformation.AssetTag,
            ImageType = ImageType!.Trim(),
            Tags = ContractText.CleanList(Tags, StringComparer.OrdinalIgnoreCase),
            Location = ContractText.TrimOrNull(Location) ?? asset.LocationAssignment.Location.LocationPath,
            Remarks = ContractText.TrimOrNull(Remarks),
            Description = ContractText.TrimOrNull(Description),
            CreatedBy = ContractText.FirstNonEmpty(CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            ClientId = ContractText.FirstNonEmpty(ClientId, currentUser?.ClientId),
            TenantId = ContractText.FirstNonEmpty(TenantId, currentUser?.TenantId),
            IsDeleted = false
        };

        file.CopyTo(image);
        return image;
    }
}

/// <summary>One uploaded image file. Width and height are the resolution read by the browser.</summary>
/// <param name="ImageName">Defaults to the file name without its extension.</param>
public sealed record AssetImageFileRequest(
    string? ImageName,
    string? FileName,
    string? FileUrl,
    string? ContentType,
    long? FileSize,
    int? Width,
    int? Height)
{
    public void CopyTo(AssetImage image)
    {
        image.FileName = FileName!.Trim();
        image.FileUrl = FileUrl!.Trim();
        image.ImageName = ContractText.TrimOrNull(ImageName) ?? Path.GetFileNameWithoutExtension(image.FileName);
        image.ContentType = ContractText.TrimOrNull(ContentType);
        image.FileSize = FileSize;
        image.Width = Width;
        image.Height = Height;
    }
}

/// <summary>Edit from the Image Details panel. Leave File empty to keep the current file.</summary>
public sealed record UpdateAssetImageRequest(
    string? AssetRegistrationId,
    string? ImageName,
    string? ImageType,
    IReadOnlyCollection<string>? Tags,
    string? Location,
    string? Remarks,
    string? Description,
    AssetImageFileRequest? File)
{
    public void ApplyTo(AssetImage image, AssetRegistration asset, CurrentUser? currentUser)
    {
        image.AssetRegistrationId = asset.AssetRegistrationId;
        image.AssetName = asset.BasicInformation.AssetName;
        image.AssetTag = asset.BasicInformation.AssetTag;

        if (File is not null && !string.IsNullOrWhiteSpace(File.FileUrl))
        {
            File.CopyTo(image);
        }

        image.ImageName = ImageName!.Trim();
        image.ImageType = ImageType!.Trim();
        image.Tags = ContractText.CleanList(Tags, StringComparer.OrdinalIgnoreCase);
        image.Location = ContractText.TrimOrNull(Location) ?? asset.LocationAssignment.Location.LocationPath;
        image.Remarks = ContractText.TrimOrNull(Remarks);
        image.Description = ContractText.TrimOrNull(Description);
        image.UpdatedBy = currentUser?.UserName;
        image.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record DeleteAssetImagesRequest(IReadOnlyCollection<string>? Ids);

/// <summary>File types and size accepted by the Upload Images form.</summary>
public static class AssetImageFiles
{
    public const long MaxFileSizeBytes = 20 * 1024 * 1024;
    public const int MaxImagesPerUpload = 50;

    public static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"];
}

/// <param name="Resolution">e.g. "4032 x 3024"; null when the size was not sent.</param>
/// <param name="UploadedBy">Who uploaded the image (CreatedBy).</param>
/// <param name="UploadDate">When it was uploaded (CreatedAt).</param>
public sealed record AssetImageResponse(
    string Id,
    string AssetRegistrationId,
    string? AssetName,
    string? AssetTag,
    string ImageName,
    string ImageType,
    IReadOnlyCollection<string> Tags,
    string? Location,
    string? Remarks,
    string? Description,
    string FileName,
    string FileUrl,
    string? ContentType,
    long? FileSize,
    int? Width,
    int? Height,
    string? Resolution,
    string? UploadedBy,
    DateTime? UploadDate,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static AssetImageResponse FromEntity(AssetImage image)
    {
        return new AssetImageResponse(
            image.Id,
            image.AssetRegistrationId,
            image.AssetName,
            image.AssetTag,
            image.ImageName,
            image.ImageType,
            image.Tags,
            image.Location,
            image.Remarks,
            image.Description,
            image.FileName,
            image.FileUrl,
            image.ContentType,
            image.FileSize,
            image.Width,
            image.Height,
            image is { Width: { } width, Height: { } height } ? $"{width} x {height}" : null,
            image.CreatedBy,
            image.CreatedAt,
            image.UpdatedBy,
            image.UpdatedAt,
            image.ClientId,
            image.TenantId);
    }
}

public sealed record AssetImageListResponse(
    IReadOnlyCollection<AssetImageResponse> Items,
    long TotalCount,
    int Page,
    int PageSize);
