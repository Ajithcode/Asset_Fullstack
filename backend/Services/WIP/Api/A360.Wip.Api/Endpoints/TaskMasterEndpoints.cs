
using A360.Wip.Api.Contracts;
using A360.Wip.Api.Validation;
using A360.Wip.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Wip.Api.Endpoints;

public static class TaskMasterEndpoints
{
    public static RouteGroupBuilder MapTaskMasterEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/task-masters")
            .WithTags("TaskMasters");

        group.MapGet("", GetTaskMastersAsync)
            .WithName("GetTaskMasters");

        group.MapGet("/{id}", GetTaskMasterByIdAsync)
            .WithName("GetTaskMasterById");

        group.MapGet("/asset/{assetId}", GetTaskMastersByAssetIdAsync)
            .WithName("GetTaskMastersByAssetId");

        group.MapGet("/job/{jobId}", GetTaskMastersByJobIdAsync)
            .WithName("GetTaskMastersByJobId");

        group.MapPost("", CreateTaskMasterAsync)
            .WithName("CreateTaskMaster");

        group.MapPut("/{id}", UpdateTaskMasterAsync)
            .WithName("UpdateTaskMaster");

        group.MapDelete("/{id}", DeleteTaskMasterAsync)
            .WithName("DeleteTaskMaster");

        return group;
    }

    private static async Task<IResult> GetTaskMastersAsync(
        ITaskMasterRepository repository,
        CancellationToken cancellationToken)
    {
        var taskMasters = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            taskMasters.Select(TaskMasterResponse.FromEntity));
    }

    private static async Task<IResult> GetTaskMasterByIdAsync(
        string id,
        ITaskMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid task id." });
        }

        var taskMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (taskMaster is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "TaskMaster",
            taskMaster.Id,
            taskMaster.TaskId,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(TaskMasterResponse.FromEntity(taskMaster));
    }

    private static async Task<IResult> GetTaskMastersByAssetIdAsync(
        string assetId,
        ITaskMasterRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var taskMasters = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            taskMasters.Select(TaskMasterResponse.FromEntity));
    }

    private static async Task<IResult> GetTaskMastersByJobIdAsync(
        string jobId,
        ITaskMasterRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return Results.BadRequest(
                new { message = "Job id is required." });
        }

        var taskMasters = await repository.GetByJobIdAsync(
            jobId,
            cancellationToken);

        return Results.Ok(
            taskMasters.Select(TaskMasterResponse.FromEntity));
    }

    private static async Task<IResult> CreateTaskMasterAsync(
        CreateTaskMasterRequest request,
        ITaskMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var taskMaster = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "TaskMaster",
            taskMaster.Id,
            taskMaster.TaskId,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/task-masters/{taskMaster.Id}",
            TaskMasterResponse.FromEntity(taskMaster));
    }

    private static async Task<IResult> UpdateTaskMasterAsync(
        string id,
        UpdateTaskMasterRequest request,
        ITaskMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid task id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var taskMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (taskMaster is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(taskMaster);

        var updated = await repository.UpdateAsync(
            id,
            taskMaster,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "TaskMaster",
            taskMaster.Id,
            taskMaster.TaskId,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(TaskMasterResponse.FromEntity(taskMaster));
    }

    private static async Task<IResult> DeleteTaskMasterAsync(
        string id,
        ITaskMasterRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid task id." });
        }

        var taskMaster = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (taskMaster is null)
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
            "TaskMaster",
            taskMaster.Id,
            taskMaster.TaskId,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
