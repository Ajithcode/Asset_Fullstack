using A360.Asset.Api.Contracts;
using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

/// <summary>Images filed against registered assets, for the Asset Images screen.</summary>
/// <remarks>View History reads GET /api/eventlogs?entityType=AssetImage&amp;entityId={id}.</remarks>
public static class AssetImageEndpoints
{
    private const string EntityType = "AssetImage";

    public static RouteGroupBuilder MapAssetImageEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-images").WithTags("Asset Images");

        group.MapGet("", GetImagesAsync).WithName("GetAssetImages");
        group.MapGet("/{id}", GetImageByIdAsync).WithName("GetAssetImageById");
        group.MapPost("", CreateImagesAsync).WithName("CreateAssetImages");
        group.MapPut("/{id}", UpdateImageAsync).WithName("UpdateAssetImage");
        group.MapDelete("/{id}", DeleteImageAsync).WithName("DeleteAssetImage");
        group.MapPost("/bulk-delete", DeleteImagesAsync).WithName("DeleteAssetImages");

        return group;
    }

    private static async Task<IResult> GetImagesAsync(
        IAssetImageRepository repository,
        CancellationToken cancellationToken,
        string? search = null,
        string? assetRegistrationId = null,
        string? imageType = null,
        int page = 1,
        int pageSize = AssetImageRepository.DefaultPageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > AssetImageRepository.MaxPageSize ? AssetImageRepository.DefaultPageSize : pageSize;

        var result = await repository.QueryAsync(
            new AssetImageQuery(search, assetRegistrationId, imageType, page, pageSize),
            cancellationToken);

        return Results.Ok(new AssetImageListResponse(
            result.Items.Select(AssetImageResponse.FromEntity).ToList(),
            result.TotalCount,
            page,
            pageSize));
    }

    private static async Task<IResult> GetImageByIdAsync(
        string id,
        IAssetImageRepository repository,
        CancellationToken cancellationToken)
    {
        var image = await repository.GetByIdAsync(id, cancellationToken);

        return image is null || image.IsDeleted
            ? ImageNotFound(id)
            : Results.Ok(AssetImageResponse.FromEntity(image));
    }

    /// <summary>Saves every uploaded image once for each selected asset.</summary>
    private static async Task<IResult> CreateImagesAsync(
        CreateAssetImagesRequest request,
        IAssetImageRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var assets = new List<AssetRegistration>();
        foreach (var assetRegistrationId in ContractText.CleanList(request.AssetRegistrationIds, StringComparer.Ordinal))
        {
            var asset = await assetRepository.GetByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken);
            if (asset is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["AssetRegistrationIds"] = [$"Asset '{assetRegistrationId}' was not found"]
                });
            }

            assets.Add(asset);
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var created = new List<AssetImageResponse>();

        foreach (var asset in assets)
        {
            foreach (var file in request.Images!)
            {
                var image = request.ToEntity(asset, file, currentUser);
                await repository.CreateAsync(image, cancellationToken);
                await eventLogger.LogAsync(EntityType, image.Id, $"{asset.AssetRegistrationId} - {image.ImageName}", EventAction.Created, cancellationToken);
                created.Add(AssetImageResponse.FromEntity(image));
            }
        }

        return Results.Created("/api/asset-images", created);
    }

    private static async Task<IResult> UpdateImageAsync(
        string id,
        UpdateAssetImageRequest request,
        IAssetImageRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var image = await repository.GetByIdAsync(id, cancellationToken);
        if (image is null || image.IsDeleted)
        {
            return ImageNotFound(id);
        }

        var assetRegistrationId = request.AssetRegistrationId!.Trim();
        var asset = await assetRepository.GetByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken);
        if (asset is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["AssetRegistrationId"] = [$"Asset '{assetRegistrationId}' was not found"]
            });
        }

        request.ApplyTo(image, asset, currentUserAccessor.GetCurrentUser());

        if (!await repository.UpdateAsync(id, image, cancellationToken))
        {
            return ImageNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, image.Id, $"{asset.AssetRegistrationId} - {image.ImageName}", EventAction.Updated, cancellationToken);

        return Results.Ok(AssetImageResponse.FromEntity(image));
    }

    private static async Task<IResult> DeleteImageAsync(
        string id,
        IAssetImageRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var image = await repository.GetByIdAsync(id, cancellationToken);
        if (image is null || image.IsDeleted || !await repository.DeleteAsync(id, cancellationToken))
        {
            return ImageNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, image.Id, $"{image.AssetRegistrationId} - {image.ImageName}", EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    /// <summary>Deletes the images ticked in the list. Ids that are not found are skipped.</summary>
    private static async Task<IResult> DeleteImagesAsync(
        DeleteAssetImagesRequest request,
        IAssetImageRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var ids = ContractText.CleanList(request.Ids, StringComparer.Ordinal);
        if (ids.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Ids"] = ["Select at least one image"]
            });
        }

        var deleted = 0;
        foreach (var id in ids)
        {
            var image = await repository.GetByIdAsync(id, cancellationToken);
            if (image is null || image.IsDeleted || !await repository.DeleteAsync(id, cancellationToken))
            {
                continue;
            }

            await eventLogger.LogAsync(EntityType, image.Id, $"{image.AssetRegistrationId} - {image.ImageName}", EventAction.Deleted, cancellationToken);
            deleted++;
        }

        return Results.Ok(new { deletedCount = deleted });
    }

    private static IResult ImageNotFound(string id)
    {
        return Results.NotFound(new { message = $"Asset image '{id}' was not found." });
    }
}
