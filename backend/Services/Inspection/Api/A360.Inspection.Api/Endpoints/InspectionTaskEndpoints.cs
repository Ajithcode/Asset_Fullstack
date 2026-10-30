using A360.Inspection.Api.Contracts;
using A360.Inspection.Api.Validation;
using A360.Inspection.Repository.Repositories;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Domain.Entities;
using A360.Repository.Activity;

namespace A360.Inspection.Api.Endpoints;

public static class InspectionTaskEndpoints
{
    private const string SequenceName = "inspection_task";
    private const string TaskCodePrefix = "TASK";

    public static RouteGroupBuilder MapInspectionTaskEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/inspection-tasks").WithTags("InspectionTasks");

        group.MapGet("", GetInspectionTasksAsync).WithName("GetInspectionTasks");
        group.MapGet("/{id}", GetInspectionTaskByIdAsync).WithName("GetInspectionTaskById");
        group.MapPost("", CreateInspectionTaskAsync).WithName("CreateInspectionTask");
        group.MapPut("/{id}", UpdateInspectionTaskAsync).WithName("UpdateInspectionTask");
        group.MapDelete("/{id}", DeleteInspectionTaskAsync).WithName("DeleteInspectionTask");

        return group;
    }

    private static async Task<IResult> GetInspectionTasksAsync(
        IInspectionTaskRepository repository,
        CancellationToken cancellationToken)
    {
        var tasks = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(tasks.Select(InspectionTaskResponse.FromEntity));
    }

    private static async Task<IResult> GetInspectionTaskByIdAsync(
        string id,
        IInspectionTaskRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid inspection task id." });
        }

        var task = await repository.GetByIdAsync(id, cancellationToken);
        if (task is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("InspectionTask", task.TaskCode, task.TaskTitle, EventAction.Viewed, cancellationToken);
        return Results.Ok(InspectionTaskResponse.FromEntity(task));
    }

    private static async Task<IResult> CreateInspectionTaskAsync(
        CreateInspectionTaskRequest request,
        IInspectionTaskRepository repository,
        ISequenceGenerator sequenceGenerator,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var nextSequence = await sequenceGenerator.GetNextValueAsync(SequenceName, cancellationToken);
        var taskCode = $"{TaskCodePrefix}{nextSequence:D6}";

        var task = await repository.CreateAsync(request.ToEntity(taskCode), cancellationToken);

        await eventLogger.LogAsync("InspectionTask", task.TaskCode, task.TaskTitle, EventAction.Created, cancellationToken);

        return Results.Created($"/api/inspection-tasks/{task.Id}", InspectionTaskResponse.FromEntity(task));
    }

    private static async Task<IResult> UpdateInspectionTaskAsync(
        string id,
        UpdateInspectionTaskRequest request,
        IInspectionTaskRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid inspection task id." });
        }

        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var task = await repository.GetByIdAsync(id, cancellationToken);
        if (task is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(task);

        var updated = await repository.UpdateAsync(id, task, cancellationToken);
        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("InspectionTask", task.TaskCode, task.TaskTitle, EventAction.Updated, cancellationToken);
        return Results.Ok(InspectionTaskResponse.FromEntity(task));
    }

    private static async Task<IResult> DeleteInspectionTaskAsync(
        string id,
        IInspectionTaskRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(new { message = "Invalid inspection task id." });
        }

        var task = await repository.GetByIdAsync(id, cancellationToken);
        if (task is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync("InspectionTask", task.TaskCode, task.TaskTitle, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }
}
