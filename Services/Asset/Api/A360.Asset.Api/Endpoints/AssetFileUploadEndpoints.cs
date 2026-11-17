using System.Globalization;
using A360.Asset.Api.Contracts;
using A360.Asset.Api.Services;
using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;
using Microsoft.AspNetCore.Mvc;

namespace A360.Asset.Api.Endpoints;

/// <summary>
/// Upload Images and Upload Document on an asset's Images / Documents tabs: one multipart call sends the file(s)
/// and the form fields, stores the files through the Media service and saves them against the asset.
/// Separate from the images and attachments entered during Asset Registration.
/// </summary>
public static class AssetFileUploadEndpoints
{
    private const string ImageEntityType = "AssetImage";
    private const string DocumentEntityType = "AssetDocument";
    private const string ImageCategory = "asset-images";
    private const string DocumentCategory = "asset-documents";

    // Placeholder that lets the existing validation run before the files are stored.
    private const string PendingUrl = "pending-upload";

    // Up to 50 images of 20 MB each in one request.
    private const long MaxImageRequestBytes = AssetImageFiles.MaxImagesPerUpload * AssetImageFiles.MaxFileSizeBytes;
    private const long MaxDocumentRequestBytes = AssetDocumentFiles.MaxFileSizeBytes + 1024 * 1024;

    public static RouteGroupBuilder MapAssetFileUploadEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-registrations/{assetRegistrationId}").WithTags("Asset Images", "Asset Documents");

        group.MapPost("/images", UploadImagesAsync)
            .WithName("UploadAssetImages")
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxImageRequestBytes), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = MaxImageRequestBytes });

        group.MapPost("/documents", UploadDocumentAsync)
            .WithName("UploadAssetDocument")
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxDocumentRequestBytes), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = MaxDocumentRequestBytes });

        return group;
    }

    /// <summary>
    /// Form fields: files (one or more), imageType (required), tags (comma separated or repeated), location, remarks,
    /// description, and imageName (only used when one file is sent; otherwise each file's name is used).
    /// </summary>
    private static async Task<IResult> UploadImagesAsync(
        string assetRegistrationId,
        HttpRequest httpRequest,
        IAssetImageRepository repository,
        IAssetRegistrationRepository assetRepository,
        IMediaUploadClient mediaUploadClient,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!httpRequest.HasFormContentType)
        {
            return Results.BadRequest(new { message = "Send the images as multipart/form-data." });
        }

        var asset = await assetRepository.GetByAssetRegistrationIdAsync(assetRegistrationId.Trim(), cancellationToken);
        if (asset is null)
        {
            return AssetNotFound(assetRegistrationId);
        }

        var form = await httpRequest.ReadFormAsync(cancellationToken);
        var files = form.Files.Where(file => file.Length > 0).ToList();
        var singleName = files.Count == 1 ? Text(form, "imageName") : null;

        AssetImageFileRequest Describe(IFormFile file, string url) => new(
            singleName,
            file.FileName,
            url,
            file.ContentType,
            file.Length,
            null,
            null);

        CreateAssetImagesRequest Build(IReadOnlyCollection<AssetImageFileRequest> images) => new(
            [asset.AssetRegistrationId],
            images,
            Text(form, "imageType"),
            Tags(form),
            Text(form, "location"),
            Text(form, "remarks"),
            Text(form, "description"),
            null,
            null,
            null);

        var errors = Build(files.Select(file => Describe(file, PendingUrl)).ToList()).Validate();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var uploaded = new List<AssetImageFileRequest>();
        foreach (var file in files)
        {
            var result = await mediaUploadClient.UploadAsync(file, ImageCategory, cancellationToken);
            if (result.Url is null)
            {
                return UploadFailed(result.Error, uploaded.Count);
            }

            uploaded.Add(Describe(file, result.Url));
        }

        var request = Build(uploaded);
        var currentUser = currentUserAccessor.GetCurrentUser();
        var created = new List<AssetImageResponse>();
        foreach (var file in uploaded)
        {
            var image = request.ToEntity(asset, file, currentUser);
            await repository.CreateAsync(image, cancellationToken);
            await eventLogger.LogAsync(ImageEntityType, image.Id, $"{asset.AssetRegistrationId} - {image.ImageName}", EventAction.Created, cancellationToken);
            created.Add(AssetImageResponse.FromEntity(image));
        }

        return Results.Created($"/api/asset-images?assetRegistrationId={Uri.EscapeDataString(asset.AssetRegistrationId)}", created);
    }

    /// <summary>
    /// Form fields: file (required), documentName, documentType, lifecycleStage, version (all required),
    /// expiryDate (yyyy-MM-dd), description, tags (comma separated or repeated), status (Active / Inactive).
    /// </summary>
    private static async Task<IResult> UploadDocumentAsync(
        string assetRegistrationId,
        HttpRequest httpRequest,
        IAssetDocumentRepository repository,
        IAssetRegistrationRepository assetRepository,
        IMediaUploadClient mediaUploadClient,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!httpRequest.HasFormContentType)
        {
            return Results.BadRequest(new { message = "Send the document as multipart/form-data." });
        }

        var asset = await assetRepository.GetByAssetRegistrationIdAsync(assetRegistrationId.Trim(), cancellationToken);
        if (asset is null)
        {
            return AssetNotFound(assetRegistrationId);
        }

        var form = await httpRequest.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is { Length: 0 })
        {
            file = null;
        }

        var errors = new Dictionary<string, string[]>();
        DateTime? expiryDate = null;
        var expiryText = Text(form, "expiryDate");
        if (expiryText is not null)
        {
            if (DateTime.TryParse(expiryText, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
            {
                expiryDate = parsed;
            }
            else
            {
                errors["ExpiryDate"] = ["ExpiryDate must be a date, e.g. 2027-01-26"];
            }
        }

        CreateAssetDocumentRequest Build(string? url) => new(
            [asset.AssetRegistrationId],
            Text(form, "documentName"),
            Text(form, "documentType"),
            Text(form, "lifecycleStage"),
            Text(form, "version"),
            expiryDate,
            Text(form, "description"),
            Tags(form),
            Text(form, "status"),
            file?.FileName,
            url,
            file?.ContentType,
            file?.Length,
            null,
            null,
            null);

        foreach (var (key, messages) in Build(file is null ? null : PendingUrl).Validate())
        {
            errors[key == "FileUrl" ? "File" : key] = key == "FileUrl" ? ["Choose a file to upload"] : messages;
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await mediaUploadClient.UploadAsync(file!, DocumentCategory, cancellationToken);
        if (result.Url is null)
        {
            return UploadFailed(result.Error, 0);
        }

        var document = Build(result.Url).ToEntity(asset, currentUserAccessor.GetCurrentUser());
        await repository.CreateAsync(document, cancellationToken);
        await eventLogger.LogAsync(DocumentEntityType, document.Id, $"{asset.AssetRegistrationId} - {document.DocumentName}", EventAction.Created, cancellationToken);

        return Results.Created($"/api/asset-documents/{document.Id}", AssetDocumentResponse.FromEntity(document));
    }

    private static string? Text(IFormCollection form, string key)
    {
        var value = form[key].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Tags sent either as one comma separated field or as repeated "tags" fields.</summary>
    private static List<string> Tags(IFormCollection form) => form["tags"]
        .SelectMany(value => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .ToList();

    private static IResult AssetNotFound(string assetRegistrationId) =>
        Results.NotFound(new { message = $"Asset registration '{assetRegistrationId}' was not found." });

    /// <param name="storedCount">Files already stored before the failure; their records are not saved.</param>
    private static IResult UploadFailed(string? error, int storedCount) =>
        Results.Problem(
            title: "Upload failed",
            detail: error ?? "The file could not be stored.",
            statusCode: StatusCodes.Status502BadGateway,
            extensions: new Dictionary<string, object?> { ["storedBeforeFailure"] = storedCount });
}
