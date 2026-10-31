
using A360.Maintenance.Api.Contracts;
using A360.Maintenance.Api.Validation;
using A360.Maintenance.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Maintenance.Api.Endpoints;

public static class PreventiveMaintenanceEndpoints
{
    public static RouteGroupBuilder MapPreventiveMaintenanceEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/preventive-maintenance")
            .WithTags("PreventiveMaintenance");

        group.MapGet("", GetPreventiveMaintenancesAsync)
            .WithName("GetPreventiveMaintenances");

        group.MapGet("/{id}", GetPreventiveMaintenanceByIdAsync)
            .WithName("GetPreventiveMaintenanceById");

        group.MapGet("/asset/{assetId}", GetPreventiveMaintenancesByAssetIdAsync)
            .WithName("GetPreventiveMaintenancesByAssetId");

        group.MapPost("", CreatePreventiveMaintenanceAsync)
            .WithName("CreatePreventiveMaintenance");

        group.MapPut("/{id}", UpdatePreventiveMaintenanceAsync)
            .WithName("UpdatePreventiveMaintenance");

        group.MapDelete("/{id}", DeletePreventiveMaintenanceAsync)
            .WithName("DeletePreventiveMaintenance");

        return group;
    }

    private static async Task<IResult> GetPreventiveMaintenancesAsync(
        IPreventiveMaintenanceRepository repository,
        CancellationToken cancellationToken)
    {
        var preventiveMaintenances = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            preventiveMaintenances.Select(PreventiveMaintenanceResponse.FromEntity));
    }

    private static async Task<IResult> GetPreventiveMaintenanceByIdAsync(
        string id,
        IPreventiveMaintenanceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid preventive maintenance id." });
        }

        var preventiveMaintenance = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (preventiveMaintenance is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "PreventiveMaintenance",
            preventiveMaintenance.Id,
            preventiveMaintenance.PmScheduleId,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(PreventiveMaintenanceResponse.FromEntity(preventiveMaintenance));
    }

    private static async Task<IResult> GetPreventiveMaintenancesByAssetIdAsync(
        string assetId,
        IPreventiveMaintenanceRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var preventiveMaintenances = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            preventiveMaintenances.Select(PreventiveMaintenanceResponse.FromEntity));
    }

    private static async Task<IResult> CreatePreventiveMaintenanceAsync(
        CreatePreventiveMaintenanceRequest request,
        IPreventiveMaintenanceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var preventiveMaintenance = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "PreventiveMaintenance",
            preventiveMaintenance.Id,
            preventiveMaintenance.PmScheduleId,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/preventive-maintenance/{preventiveMaintenance.Id}",
            PreventiveMaintenanceResponse.FromEntity(preventiveMaintenance));
    }

    private static async Task<IResult> UpdatePreventiveMaintenanceAsync(
        string id,
        UpdatePreventiveMaintenanceRequest request,
        IPreventiveMaintenanceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid preventive maintenance id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var preventiveMaintenance = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (preventiveMaintenance is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(preventiveMaintenance);

        var updated = await repository.UpdateAsync(
            id,
            preventiveMaintenance,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "PreventiveMaintenance",
            preventiveMaintenance.Id,
            preventiveMaintenance.PmScheduleId,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(PreventiveMaintenanceResponse.FromEntity(preventiveMaintenance));
    }

    private static async Task<IResult> DeletePreventiveMaintenanceAsync(
        string id,
        IPreventiveMaintenanceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid preventive maintenance id." });
        }

        var preventiveMaintenance = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (preventiveMaintenance is null)
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
            "PreventiveMaintenance",
            preventiveMaintenance.Id,
            preventiveMaintenance.PmScheduleId,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
