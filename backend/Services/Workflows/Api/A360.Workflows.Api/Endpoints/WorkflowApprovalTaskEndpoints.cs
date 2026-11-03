
using A360.Workflows.Api.Contracts;
using A360.Workflows.Api.Validation;
using A360.Workflows.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Workflows.Api.Endpoints;

public static class WorkflowApprovalTaskEndpoints
{
    public static RouteGroupBuilder MapWorkflowApprovalTaskEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/workflow-approval-tasks")
            .WithTags("WorkflowApprovalTasks");

        group.MapGet("", GetWorkflowApprovalTasksAsync)
            .WithName("GetWorkflowApprovalTasks");

        group.MapGet("/{id}", GetWorkflowApprovalTaskByIdAsync)
            .WithName("GetWorkflowApprovalTaskById");

        group.MapGet("/my-tasks/{assignedTo}", GetMyWorkflowApprovalTasksAsync)
            .WithName("GetMyWorkflowApprovalTasks");

        group.MapPost("", CreateWorkflowApprovalTaskAsync)
            .WithName("CreateWorkflowApprovalTask");

        group.MapPut("/{id}", UpdateWorkflowApprovalTaskAsync)
            .WithName("UpdateWorkflowApprovalTask");

        group.MapDelete("/{id}", DeleteWorkflowApprovalTaskAsync)
            .WithName("DeleteWorkflowApprovalTask");

        return group;
    }

    private static async Task<IResult> GetWorkflowApprovalTasksAsync(
        IWorkflowApprovalTaskRepository repository,
        CancellationToken cancellationToken)
    {
        var workflowApprovalTasks = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            workflowApprovalTasks.Select(WorkflowApprovalTaskResponse.FromEntity));
    }

    private static async Task<IResult> GetWorkflowApprovalTaskByIdAsync(
        string id,
        IWorkflowApprovalTaskRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid workflow approval task id." });
        }

        var workflowApprovalTask = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workflowApprovalTask is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "WorkflowApprovalTask",
            workflowApprovalTask.Id,
            workflowApprovalTask.WorkflowName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(WorkflowApprovalTaskResponse.FromEntity(workflowApprovalTask));
    }

    private static async Task<IResult> GetMyWorkflowApprovalTasksAsync(
        string assignedTo,
        IWorkflowApprovalTaskRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assignedTo))
        {
            return Results.BadRequest(
                new { message = "AssignedTo is required." });
        }

        var workflowApprovalTasks = await repository.GetByAssignedToAsync(
            assignedTo,
            cancellationToken);

        return Results.Ok(
            workflowApprovalTasks.Select(WorkflowApprovalTaskResponse.FromEntity));
    }

    private static async Task<IResult> CreateWorkflowApprovalTaskAsync(
        CreateWorkflowApprovalTaskRequest request,
        IWorkflowApprovalTaskRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var workflowApprovalTask = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "WorkflowApprovalTask",
            workflowApprovalTask.Id,
            workflowApprovalTask.WorkflowName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/workflow-approval-tasks/{workflowApprovalTask.Id}",
            WorkflowApprovalTaskResponse.FromEntity(workflowApprovalTask));
    }

    private static async Task<IResult> UpdateWorkflowApprovalTaskAsync(
        string id,
        UpdateWorkflowApprovalTaskRequest request,
        IWorkflowApprovalTaskRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid workflow approval task id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var workflowApprovalTask = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workflowApprovalTask is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(workflowApprovalTask);

        var updated = await repository.UpdateAsync(
            id,
            workflowApprovalTask,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "WorkflowApprovalTask",
            workflowApprovalTask.Id,
            workflowApprovalTask.WorkflowName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(WorkflowApprovalTaskResponse.FromEntity(workflowApprovalTask));
    }

    private static async Task<IResult> DeleteWorkflowApprovalTaskAsync(
        string id,
        IWorkflowApprovalTaskRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid workflow approval task id." });
        }

        var workflowApprovalTask = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workflowApprovalTask is null)
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
            "WorkflowApprovalTask",
            workflowApprovalTask.Id,
            workflowApprovalTask.WorkflowName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
