
using A360.Wip.Api.Contracts;
using A360.Wip.Api.Validation;
using A360.Wip.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Wip.Api.Endpoints;

public static class SlaMasterEndpoints
{
    public static RouteGroupBuilder MapSlaMasterEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/sla-masters")
            .WithTags("SlaMasters");

        group.MapGet("", GetSlaMastersAsync)
            .WithName("GetSlaMasters");

        group.MapGet("/{id}", GetSlaMasterByIdAsync)
            .WithName("GetSlaMasterById");

        group.MapGet("/asset/{assetId}", GetSlaMastersByAssetIdAsync)
            .WithName("GetSlaMastersByAssetId");

        group.MapPost("", CreateSlaMasterAsync)
            .WithName("CreateSlaMaster");

        group.MapPut("/{id}", UpdateSlaMasterAsync)
            .WithName("UpdateSlaMaster");

        group.MapDelete("/{id}", DeleteSlaMasterAsync)
            .WithName("DeleteSlaMaster");

        return group;
    }

    private static async Task<IResult> GetSlaMastersAsync(
        ISlaMasterRepository repository,
        CancellationToken cancellationToken)
    {
        var slaMasters = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            slaMasters.Select(SlaMasterResponse.FromEntity));
    }

    private static async Task<IResult> GetSlaMasterByIdAsync(
        string id,
        ISlaMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid sla id." });
        }

        var slaMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (slaMaster is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "SlaMaster",
            slaMaster.Id,
            slaMaster.SlaId,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(SlaMasterResponse.FromEntity(slaMaster));
    }

    private static async Task<IResult> GetSlaMastersByAssetIdAsync(
        string assetId,
        ISlaMasterRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var slaMasters = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            slaMasters.Select(SlaMasterResponse.FromEntity));
    }

    private static async Task<IResult> CreateSlaMasterAsync(
        CreateSlaMasterRequest request,
        ISlaMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var slaMaster = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "SlaMaster",
            slaMaster.Id,
            slaMaster.SlaId,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/sla-masters/{slaMaster.Id}",
            SlaMasterResponse.FromEntity(slaMaster));
    }

    private static async Task<IResult> UpdateSlaMasterAsync(
        string id,
        UpdateSlaMasterRequest request,
        ISlaMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid sla id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var slaMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (slaMaster is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(slaMaster);

        var updated = await repository.UpdateAsync(
            id,
            slaMaster,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "SlaMaster",
            slaMaster.Id,
            slaMaster.SlaId,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(SlaMasterResponse.FromEntity(slaMaster));
    }

    private static async Task<IResult> DeleteSlaMasterAsync(
        string id,
        ISlaMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid sla id." });
        }

        var slaMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (slaMaster is null)
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
            "SlaMaster",
            slaMaster.Id,
            slaMaster.SlaId,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
