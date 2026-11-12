using A360.Asset.Api.Contracts;
using A360.Asset.Repository.Repositories;

namespace A360.Asset.Api.Endpoints;

/// <summary>All images and documents of one asset in a single call, for the asset details screen.</summary>
public static class AssetFilesEndpoints
{
    public static RouteGroupBuilder MapAssetFilesEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-registrations").WithTags("Asset Registration");

        group.MapGet("/{assetRegistrationId}/files", GetAssetFilesAsync).WithName("GetAssetFiles");

        return group;
    }

    private static async Task<IResult> GetAssetFilesAsync(
        string assetRegistrationId,
        IAssetRegistrationRepository assetRegistrationRepository,
        IAssetImageRepository imageRepository,
        IAssetDocumentRepository documentRepository,
        CancellationToken cancellationToken)
    {
        var asset = await assetRegistrationRepository.GetByAssetRegistrationIdAsync(assetRegistrationId.Trim(), cancellationToken);
        if (asset is null)
        {
            return Results.NotFound(new { message = $"Asset registration '{assetRegistrationId}' was not found." });
        }

        var images = await imageRepository.GetByAssetRegistrationIdAsync(asset.AssetRegistrationId, cancellationToken);
        var documents = await documentRepository.GetByAssetRegistrationIdAsync(asset.AssetRegistrationId, cancellationToken);

        var registrationAttachments = asset.PurchaseWarranty.Documents
            .Select(document => AssetRegistrationAttachmentResponse.FromEntity(document, AssetRegistrationAttachmentResponse.PurchaseWarrantySource))
            .Concat(asset.AdditionalInformation.Attachments
                .Select(document => AssetRegistrationAttachmentResponse.FromEntity(document, AssetRegistrationAttachmentResponse.AdditionalInformationSource)))
            .ToList();

        return Results.Ok(new AssetFilesResponse(
            asset.AssetRegistrationId,
            asset.BasicInformation.AssetName,
            asset.BasicInformation.AssetTag,
            images.Select(AssetImageResponse.FromEntity).ToList(),
            documents.Select(AssetDocumentResponse.FromEntity).ToList(),
            registrationAttachments,
            images.Count,
            documents.Count,
            registrationAttachments.Count));
    }
}
