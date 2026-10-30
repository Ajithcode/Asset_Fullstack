
using A360.Maintenance.Api.Contracts;
using A360.Maintenance.Api.Validation;
using A360.Maintenance.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Maintenance.Api.Endpoints;

public static class MaintenanceTaskEndpoints
{
    public static RouteGroupBuilder MapMaintenanceTaskEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/maintenance-tasks")
            .WithTags("MaintenanceTasks");

        group.MapGet("", GetMaintenanceTasksAsync)
            .WithName("GetMaintenanceTasks");

        group.MapGet("/{id}", GetMaintenanceTaskByIdAsync)
            .WithName("GetMaintenanceTaskById");

        group.MapGet("/asset/{assetId}", GetMaintenanceTasksByAssetIdAsync)
            .WithName("GetMaintenanceTasksByAssetId");

        group.MapPost("", CreateMaintenanceTaskAsync)
            .WithName("CreateMaintenanceTask");

        group.MapPut("/{id}", UpdateMaintenanceTaskAsync)
            .WithName("UpdateMaintenanceTask");

        group.MapDelete("/{id}", DeleteMaintenanceTaskAsync)
            .WithName("DeleteMaintenanceTask");

        return group;
    }

    private static async Task<IResult> GetMaintenanceTasksAsync(
        IMaintenanceTaskRepository repository,
        CancellationToken cancellationToken)
    {
        var maintenanceTasks = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            maintenanceTasks.Select(MaintenanceTaskResponse.FromEntity));
    }

    private static async Task<IResult> GetMaintenanceTaskByIdAsync(
        string id,
        IMaintenanceTaskRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid maintenance task id." });
        }

        var maintenanceTask = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (maintenanceTask is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "MaintenanceTask",
            maintenanceTask.Id,
            maintenanceTask.AssetName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(MaintenanceTaskResponse.FromEntity(maintenanceTask));
    }

    private static async Task<IResult> GetMaintenanceTasksByAssetIdAsync(
        string assetId,
        IMaintenanceTaskRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var maintenanceTasks = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            maintenanceTasks.Select(MaintenanceTaskResponse.FromEntity));
    }

    private static async Task<IResult> CreateMaintenanceTaskAsync(
        CreateMaintenanceTaskRequest request,
        IMaintenanceTaskRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var maintenanceTask = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "MaintenanceTask",
            maintenanceTask.Id,
            maintenanceTask.AssetName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/maintenance-tasks/{maintenanceTask.Id}",
            MaintenanceTaskResponse.FromEntity(maintenanceTask));
    }

    private static async Task<IResult> UpdateMaintenanceTaskAsync(
        string id,
        UpdateMaintenanceTaskRequest request,
        IMaintenanceTaskRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid maintenance task id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var maintenanceTask = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (maintenanceTask is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(maintenanceTask);

        var updated = await repository.UpdateAsync(
            id,
            maintenanceTask,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "MaintenanceTask",
            maintenanceTask.Id,
            maintenanceTask.AssetName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(MaintenanceTaskResponse.FromEntity(maintenanceTask));
    }

    private static async Task<IResult> DeleteMaintenanceTaskAsync(
        string id,
        IMaintenanceTaskRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid maintenance task id." });
        }

        var maintenanceTask = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (maintenanceTask is null)
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
            "MaintenanceTask",
            maintenanceTask.Id,
            maintenanceTask.AssetName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
