
using A360.Wip.Api.Contracts;
using A360.Wip.Api.Validation;
using A360.Wip.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Wip.Api.Endpoints;

public static class AssetLinkingEndpoints
{
    public static RouteGroupBuilder MapAssetLinkingEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-linkings")
            .WithTags("AssetLinkings");

        group.MapGet("", GetAssetLinkingsAsync)
            .WithName("GetAssetLinkings");

        group.MapGet("/{id}", GetAssetLinkingByIdAsync)
            .WithName("GetAssetLinkingById");

        group.MapGet("/asset/{assetId}", GetAssetLinkingByAssetIdAsync)
            .WithName("GetAssetLinkingByAssetId");

        group.MapPost("", CreateAssetLinkingAsync)
            .WithName("CreateAssetLinking");

        group.MapPut("/{id}", UpdateAssetLinkingAsync)
            .WithName("UpdateAssetLinking");

        group.MapDelete("/{id}", DeleteAssetLinkingAsync)
            .WithName("DeleteAssetLinking");

        return group;
    }

    private static async Task<IResult> GetAssetLinkingsAsync(
        IAssetLinkingRepository repository,
        CancellationToken cancellationToken)
    {
        var assetLinkings = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            assetLinkings.Select(AssetLinkingResponse.FromEntity));
    }

    private static async Task<IResult> GetAssetLinkingByIdAsync(
        string id,
        IAssetLinkingRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid asset linking id." });
        }

        var assetLinking = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (assetLinking is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "AssetLinking",
            assetLinking.Id,
            assetLinking.AssetId,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(AssetLinkingResponse.FromEntity(assetLinking));
    }

    private static async Task<IResult> GetAssetLinkingByAssetIdAsync(
        string assetId,
        IAssetLinkingRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var assetLinking = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        if (assetLinking is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(AssetLinkingResponse.FromEntity(assetLinking));
    }

    private static async Task<IResult> CreateAssetLinkingAsync(
        CreateAssetLinkingRequest request,
        IAssetLinkingRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var assetLinking = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "AssetLinking",
            assetLinking.Id,
            assetLinking.AssetId,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/asset-linkings/{assetLinking.Id}",
            AssetLinkingResponse.FromEntity(assetLinking));
    }

    private static async Task<IResult> UpdateAssetLinkingAsync(
        string id,
        UpdateAssetLinkingRequest request,
        IAssetLinkingRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid asset linking id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var assetLinking = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (assetLinking is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(assetLinking);

        var updated = await repository.UpdateAsync(
            id,
            assetLinking,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "AssetLinking",
            assetLinking.Id,
            assetLinking.AssetId,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(AssetLinkingResponse.FromEntity(assetLinking));
    }

    private static async Task<IResult> DeleteAssetLinkingAsync(
        string id,
        IAssetLinkingRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid asset linking id." });
        }

        var assetLinking = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (assetLinking is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "AssetLinking",
            assetLinking.Id,
            assetLinking.AssetId,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
