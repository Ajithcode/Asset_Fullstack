
using A360.Wip.Api.Contracts;
using A360.Wip.Api.Validation;
using A360.Wip.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Wip.Api.Endpoints;

public static class ResourceMasterEndpoints
{
    public static RouteGroupBuilder MapResourceMasterEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/resource-masters")
            .WithTags("ResourceMasters");

        group.MapGet("", GetResourceMastersAsync)
            .WithName("GetResourceMasters");

        group.MapGet("/{id}", GetResourceMasterByIdAsync)
            .WithName("GetResourceMasterById");

        group.MapGet("/asset/{assetId}", GetResourceMastersByAssetIdAsync)
            .WithName("GetResourceMastersByAssetId");

        group.MapPost("", CreateResourceMasterAsync)
            .WithName("CreateResourceMaster");

        group.MapPut("/{id}", UpdateResourceMasterAsync)
            .WithName("UpdateResourceMaster");

        group.MapDelete("/{id}", DeleteResourceMasterAsync)
            .WithName("DeleteResourceMaster");

        return group;
    }

    private static async Task<IResult> GetResourceMastersAsync(
        IResourceMasterRepository repository,
        CancellationToken cancellationToken)
    {
        var resourceMasters = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            resourceMasters.Select(ResourceMasterResponse.FromEntity));
    }

    private static async Task<IResult> GetResourceMasterByIdAsync(
        string id,
        IResourceMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid resource id." });
        }

        var resourceMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (resourceMaster is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ResourceMaster",
            resourceMaster.Id,
            resourceMaster.ResourceId,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(ResourceMasterResponse.FromEntity(resourceMaster));
    }

    private static async Task<IResult> GetResourceMastersByAssetIdAsync(
        string assetId,
        IResourceMasterRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var resourceMasters = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            resourceMasters.Select(ResourceMasterResponse.FromEntity));
    }

    private static async Task<IResult> CreateResourceMasterAsync(
        CreateResourceMasterRequest request,
        IResourceMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var resourceMaster = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "ResourceMaster",
            resourceMaster.Id,
            resourceMaster.ResourceId,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/resource-masters/{resourceMaster.Id}",
            ResourceMasterResponse.FromEntity(resourceMaster));
    }

    private static async Task<IResult> UpdateResourceMasterAsync(
        string id,
        UpdateResourceMasterRequest request,
        IResourceMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid resource id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var resourceMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (resourceMaster is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(resourceMaster);

        var updated = await repository.UpdateAsync(
            id,
            resourceMaster,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ResourceMaster",
            resourceMaster.Id,
            resourceMaster.ResourceId,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ResourceMasterResponse.FromEntity(resourceMaster));
    }

    private static async Task<IResult> DeleteResourceMasterAsync(
        string id,
        IResourceMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid resource id." });
        }

        var resourceMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (resourceMaster is null)
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
            "ResourceMaster",
            resourceMaster.Id,
            resourceMaster.ResourceId,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
