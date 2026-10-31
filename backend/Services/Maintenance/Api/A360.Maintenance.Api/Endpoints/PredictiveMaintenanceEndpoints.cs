
using A360.Maintenance.Api.Contracts;
using A360.Maintenance.Api.Validation;
using A360.Maintenance.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Maintenance.Api.Endpoints;

public static class PredictiveMaintenanceEndpoints
{
    public static RouteGroupBuilder MapPredictiveMaintenanceEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/predictive-maintenance")
            .WithTags("PredictiveMaintenance");

        group.MapGet("", GetPredictiveMaintenancesAsync)
            .WithName("GetPredictiveMaintenances");

        group.MapGet("/{id}", GetPredictiveMaintenanceByIdAsync)
            .WithName("GetPredictiveMaintenanceById");

        group.MapGet("/asset/{assetId}", GetPredictiveMaintenancesByAssetIdAsync)
            .WithName("GetPredictiveMaintenancesByAssetId");

        group.MapPost("", CreatePredictiveMaintenanceAsync)
            .WithName("CreatePredictiveMaintenance");

        group.MapPut("/{id}", UpdatePredictiveMaintenanceAsync)
            .WithName("UpdatePredictiveMaintenance");

        group.MapDelete("/{id}", DeletePredictiveMaintenanceAsync)
            .WithName("DeletePredictiveMaintenance");

        return group;
    }

    private static async Task<IResult> GetPredictiveMaintenancesAsync(
        IPredictiveMaintenanceRepository repository,
        CancellationToken cancellationToken)
    {
        var predictiveMaintenances = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            predictiveMaintenances.Select(PredictiveMaintenanceResponse.FromEntity));
    }

    private static async Task<IResult> GetPredictiveMaintenanceByIdAsync(
        string id,
        IPredictiveMaintenanceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid predictivemaintenance id." });
        }

        var predictiveMaintenance = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (predictiveMaintenance is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "PredictiveMaintenance",
            predictiveMaintenance.Id,
            predictiveMaintenance.RiskLevel,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(PredictiveMaintenanceResponse.FromEntity(predictiveMaintenance));
    }

    private static async Task<IResult> GetPredictiveMaintenancesByAssetIdAsync(
        string assetId,
        IPredictiveMaintenanceRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var predictiveMaintenances = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            predictiveMaintenances.Select(PredictiveMaintenanceResponse.FromEntity));
    }

    private static async Task<IResult> CreatePredictiveMaintenanceAsync(
        CreatePredictiveMaintenanceRequest request,
        IPredictiveMaintenanceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var predictiveMaintenance = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "PredictiveMaintenance",
            predictiveMaintenance.Id,
            predictiveMaintenance.RiskLevel,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/predictive-maintenance/{predictiveMaintenance.Id}",
            PredictiveMaintenanceResponse.FromEntity(predictiveMaintenance));
    }

    private static async Task<IResult> UpdatePredictiveMaintenanceAsync(
        string id,
        UpdatePredictiveMaintenanceRequest request,
        IPredictiveMaintenanceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid predictivemaintenance id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var predictiveMaintenance = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (predictiveMaintenance is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(predictiveMaintenance);

        var updated = await repository.UpdateAsync(
            id,
            predictiveMaintenance,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "PredictiveMaintenance",
            predictiveMaintenance.Id,
            predictiveMaintenance.RiskLevel,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(PredictiveMaintenanceResponse.FromEntity(predictiveMaintenance));
    }

    private static async Task<IResult> DeletePredictiveMaintenanceAsync(
        string id,
        IPredictiveMaintenanceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid predictivemaintenance id." });
        }

        var predictiveMaintenance = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (predictiveMaintenance is null)
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
            "PredictiveMaintenance",
            predictiveMaintenance.Id,
            predictiveMaintenance.RiskLevel,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
