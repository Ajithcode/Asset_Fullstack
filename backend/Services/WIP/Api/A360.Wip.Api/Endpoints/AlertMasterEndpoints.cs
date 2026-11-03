
using A360.Wip.Api.Contracts;
using A360.Wip.Api.Validation;
using A360.Wip.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Wip.Api.Endpoints;

public static class AlertMasterEndpoints
{
    public static RouteGroupBuilder MapAlertMasterEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/alert-masters")
            .WithTags("AlertMasters");

        group.MapGet("", GetAlertMastersAsync)
            .WithName("GetAlertMasters");

        group.MapGet("/{id}", GetAlertMasterByIdAsync)
            .WithName("GetAlertMasterById");

        group.MapGet("/asset/{assetId}", GetAlertMastersByAssetIdAsync)
            .WithName("GetAlertMastersByAssetId");

        group.MapPost("", CreateAlertMasterAsync)
            .WithName("CreateAlertMaster");

        group.MapPut("/{id}", UpdateAlertMasterAsync)
            .WithName("UpdateAlertMaster");

        group.MapDelete("/{id}", DeleteAlertMasterAsync)
            .WithName("DeleteAlertMaster");

        return group;
    }

    private static async Task<IResult> GetAlertMastersAsync(
        IAlertMasterRepository repository,
        CancellationToken cancellationToken)
    {
        var alertMasters = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            alertMasters.Select(AlertMasterResponse.FromEntity));
    }

    private static async Task<IResult> GetAlertMasterByIdAsync(
        string id,
        IAlertMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid alert id." });
        }

        var alertMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (alertMaster is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "AlertMaster",
            alertMaster.Id,
            alertMaster.AlertId,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(AlertMasterResponse.FromEntity(alertMaster));
    }

    private static async Task<IResult> GetAlertMastersByAssetIdAsync(
        string assetId,
        IAlertMasterRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var alertMasters = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            alertMasters.Select(AlertMasterResponse.FromEntity));
    }

    private static async Task<IResult> CreateAlertMasterAsync(
        CreateAlertMasterRequest request,
        IAlertMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var alertMaster = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "AlertMaster",
            alertMaster.Id,
            alertMaster.AlertId,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/alert-masters/{alertMaster.Id}",
            AlertMasterResponse.FromEntity(alertMaster));
    }

    private static async Task<IResult> UpdateAlertMasterAsync(
        string id,
        UpdateAlertMasterRequest request,
        IAlertMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid alert id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var alertMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (alertMaster is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(alertMaster);

        var updated = await repository.UpdateAsync(
            id,
            alertMaster,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "AlertMaster",
            alertMaster.Id,
            alertMaster.AlertId,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(AlertMasterResponse.FromEntity(alertMaster));
    }

    private static async Task<IResult> DeleteAlertMasterAsync(
        string id,
        IAlertMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid alert id." });
        }

        var alertMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (alertMaster is null)
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
            "AlertMaster",
            alertMaster.Id,
            alertMaster.AlertId,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
