
using A360.Workflows.Api.Contracts;
using A360.Workflows.Api.Validation;
using A360.Workflows.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Workflows.Api.Endpoints;

public static class WorkflowInstanceEndpoints
{
    public static RouteGroupBuilder MapWorkflowInstanceEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/workflow-instances")
            .WithTags("WorkflowInstances");

        group.MapGet("", GetWorkflowInstancesAsync)
            .WithName("GetWorkflowInstances");

        group.MapGet("/{id}", GetWorkflowInstanceByIdAsync)
            .WithName("GetWorkflowInstanceById");

        group.MapGet("/workflow/{workflowName}", GetWorkflowInstancesByWorkflowNameAsync)
            .WithName("GetWorkflowInstancesByWorkflowName");

        group.MapPost("", CreateWorkflowInstanceAsync)
            .WithName("CreateWorkflowInstance");

        group.MapPut("/{id}", UpdateWorkflowInstanceAsync)
            .WithName("UpdateWorkflowInstance");

        group.MapDelete("/{id}", DeleteWorkflowInstanceAsync)
            .WithName("DeleteWorkflowInstance");

        return group;
    }

    private static async Task<IResult> GetWorkflowInstancesAsync(
        IWorkflowInstanceRepository repository,
        CancellationToken cancellationToken)
    {
        var workflowInstances = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            workflowInstances.Select(WorkflowInstanceResponse.FromEntity));
    }

    private static async Task<IResult> GetWorkflowInstanceByIdAsync(
        string id,
        IWorkflowInstanceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid workflow instance id." });
        }

        var workflowInstance = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workflowInstance is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "WorkflowInstance",
            workflowInstance.Id,
            workflowInstance.WorkflowName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(WorkflowInstanceResponse.FromEntity(workflowInstance));
    }

    private static async Task<IResult> GetWorkflowInstancesByWorkflowNameAsync(
        string workflowName,
        IWorkflowInstanceRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(workflowName))
        {
            return Results.BadRequest(
                new { message = "Workflow name is required." });
        }

        var workflowInstances = await repository.GetByWorkflowNameAsync(
            workflowName,
            cancellationToken);

        return Results.Ok(
            workflowInstances.Select(WorkflowInstanceResponse.FromEntity));
    }

    private static async Task<IResult> CreateWorkflowInstanceAsync(
        CreateWorkflowInstanceRequest request,
        IWorkflowInstanceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var workflowInstance = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "WorkflowInstance",
            workflowInstance.Id,
            workflowInstance.WorkflowName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/workflow-instances/{workflowInstance.Id}",
            WorkflowInstanceResponse.FromEntity(workflowInstance));
    }

    private static async Task<IResult> UpdateWorkflowInstanceAsync(
        string id,
        UpdateWorkflowInstanceRequest request,
        IWorkflowInstanceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid workflow instance id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var workflowInstance = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workflowInstance is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(workflowInstance);

        var updated = await repository.UpdateAsync(
            id,
            workflowInstance,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "WorkflowInstance",
            workflowInstance.Id,
            workflowInstance.WorkflowName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(WorkflowInstanceResponse.FromEntity(workflowInstance));
    }

    private static async Task<IResult> DeleteWorkflowInstanceAsync(
        string id,
        IWorkflowInstanceRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid workflow instance id." });
        }

        var workflowInstance = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workflowInstance is null)
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
            "WorkflowInstance",
            workflowInstance.Id,
            workflowInstance.WorkflowName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
