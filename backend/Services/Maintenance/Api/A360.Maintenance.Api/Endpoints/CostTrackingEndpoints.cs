
using A360.Maintenance.Api.Contracts;
using A360.Maintenance.Api.Validation;
using A360.Maintenance.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Maintenance.Api.Endpoints;

public static class CostTrackingEndpoints
{
    public static RouteGroupBuilder MapCostTrackingEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/cost-tracking")
            .WithTags("CostTracking");

        group.MapGet("", GetCostTrackingsAsync)
            .WithName("GetCostTrackings");

        group.MapGet("/{id}", GetCostTrackingByIdAsync)
            .WithName("GetCostTrackingById");

        group.MapGet("/asset/{assetId}", GetCostTrackingsByAssetIdAsync)
            .WithName("GetCostTrackingsByAssetId");

        group.MapPost("", CreateCostTrackingAsync)
            .WithName("CreateCostTracking");

        group.MapPut("/{id}", UpdateCostTrackingAsync)
            .WithName("UpdateCostTracking");

        group.MapDelete("/{id}", DeleteCostTrackingAsync)
            .WithName("DeleteCostTracking");

        return group;
    }

    private static async Task<IResult> GetCostTrackingsAsync(
        ICostTrackingRepository repository,
        CancellationToken cancellationToken)
    {
        var costTrackings = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            costTrackings.Select(CostTrackingResponse.FromEntity));
    }

    private static async Task<IResult> GetCostTrackingByIdAsync(
        string id,
        ICostTrackingRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid costtracking id." });
        }

        var costTracking = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (costTracking is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "CostTracking",
            costTracking.Id,
            costTracking.AssetName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(CostTrackingResponse.FromEntity(costTracking));
    }

    private static async Task<IResult> GetCostTrackingsByAssetIdAsync(
        string assetId,
        ICostTrackingRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var costTrackings = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            costTrackings.Select(CostTrackingResponse.FromEntity));
    }

    private static async Task<IResult> CreateCostTrackingAsync(
        CreateCostTrackingRequest request,
        ICostTrackingRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var costTracking = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "CostTracking",
            costTracking.Id,
            costTracking.AssetName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/cost-tracking/{costTracking.Id}",
            CostTrackingResponse.FromEntity(costTracking));
    }

    private static async Task<IResult> UpdateCostTrackingAsync(
        string id,
        UpdateCostTrackingRequest request,
        ICostTrackingRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid costtracking id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var costTracking = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (costTracking is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(costTracking);

        var updated = await repository.UpdateAsync(
            id,
            costTracking,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "CostTracking",
            costTracking.Id,
            costTracking.AssetName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(CostTrackingResponse.FromEntity(costTracking));
    }

    private static async Task<IResult> DeleteCostTrackingAsync(
        string id,
        ICostTrackingRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid costtracking id." });
        }

        var costTracking = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (costTracking is null)
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
            "CostTracking",
            costTracking.Id,
            costTracking.AssetName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
