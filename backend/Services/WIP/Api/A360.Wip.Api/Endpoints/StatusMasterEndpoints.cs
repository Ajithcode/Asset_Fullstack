
using A360.Wip.Api.Contracts;
using A360.Wip.Api.Validation;
using A360.Wip.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Wip.Api.Endpoints;

public static class StatusMasterEndpoints
{
    public static RouteGroupBuilder MapStatusMasterEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/wip-status-masters")
            .WithTags("StatusMasters");

        group.MapGet("", GetStatusMastersAsync)
            .WithName("GetStatusMasters");

        group.MapGet("/{id}", GetStatusMasterByIdAsync)
            .WithName("GetStatusMasterById");

        group.MapGet("/asset/{assetId}", GetStatusMastersByAssetIdAsync)
            .WithName("GetStatusMastersByAssetId");

        group.MapPost("", CreateStatusMasterAsync)
            .WithName("CreateStatusMaster");

        group.MapPut("/{id}", UpdateStatusMasterAsync)
            .WithName("UpdateStatusMaster");

        group.MapDelete("/{id}", DeleteStatusMasterAsync)
            .WithName("DeleteStatusMaster");

        return group;
    }

    private static async Task<IResult> GetStatusMastersAsync(
        IStatusMasterRepository repository,
        CancellationToken cancellationToken)
    {
        var statusMasters = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            statusMasters.Select(StatusMasterResponse.FromEntity));
    }

    private static async Task<IResult> GetStatusMasterByIdAsync(
        string id,
        IStatusMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid status id." });
        }

        var statusMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (statusMaster is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "StatusMaster",
            statusMaster.Id,
            statusMaster.StatusId,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(StatusMasterResponse.FromEntity(statusMaster));
    }

    private static async Task<IResult> GetStatusMastersByAssetIdAsync(
        string assetId,
        IStatusMasterRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var statusMasters = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            statusMasters.Select(StatusMasterResponse.FromEntity));
    }

    private static async Task<IResult> CreateStatusMasterAsync(
        CreateStatusMasterRequest request,
        IStatusMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var statusMaster = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "StatusMaster",
            statusMaster.Id,
            statusMaster.StatusId,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/wip-status-masters/{statusMaster.Id}",
            StatusMasterResponse.FromEntity(statusMaster));
    }

    private static async Task<IResult> UpdateStatusMasterAsync(
        string id,
        UpdateStatusMasterRequest request,
        IStatusMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid status id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var statusMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (statusMaster is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(statusMaster);

        var updated = await repository.UpdateAsync(
            id,
            statusMaster,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "StatusMaster",
            statusMaster.Id,
            statusMaster.StatusId,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(StatusMasterResponse.FromEntity(statusMaster));
    }

    private static async Task<IResult> DeleteStatusMasterAsync(
        string id,
        IStatusMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid status id." });
        }

        var statusMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (statusMaster is null)
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
            "StatusMaster",
            statusMaster.Id,
            statusMaster.StatusId,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
