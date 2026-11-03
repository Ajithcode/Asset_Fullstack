
using A360.Workflows.Api.Contracts;
using A360.Workflows.Api.Validation;
using A360.Workflows.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Workflows.Api.Endpoints;

public static class WorkflowBuilderEndpoints
{
    public static RouteGroupBuilder MapWorkflowBuilderEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/workflow-builders")
            .WithTags("WorkflowBuilders");

        group.MapGet("", GetWorkflowBuildersAsync)
            .WithName("GetWorkflowBuilders");

        group.MapGet("/{id}", GetWorkflowBuilderByIdAsync)
            .WithName("GetWorkflowBuilderById");

        group.MapGet("/module/{module}", GetWorkflowBuildersByModuleAsync)
            .WithName("GetWorkflowBuildersByModule");

        group.MapPost("", CreateWorkflowBuilderAsync)
            .WithName("CreateWorkflowBuilder");

        group.MapPut("/{id}", UpdateWorkflowBuilderAsync)
            .WithName("UpdateWorkflowBuilder");

        group.MapDelete("/{id}", DeleteWorkflowBuilderAsync)
            .WithName("DeleteWorkflowBuilder");

        return group;
    }

    private static async Task<IResult> GetWorkflowBuildersAsync(
        IWorkflowBuilderRepository repository,
        CancellationToken cancellationToken)
    {
        var workflowBuilders = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            workflowBuilders.Select(WorkflowBuilderResponse.FromEntity));
    }

    private static async Task<IResult> GetWorkflowBuilderByIdAsync(
        string id,
        IWorkflowBuilderRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid workflow builder id." });
        }

        var workflowBuilder = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workflowBuilder is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "WorkflowBuilder",
            workflowBuilder.Id,
            workflowBuilder.WorkflowName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(WorkflowBuilderResponse.FromEntity(workflowBuilder));
    }

    private static async Task<IResult> GetWorkflowBuildersByModuleAsync(
        string module,
        IWorkflowBuilderRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            return Results.BadRequest(
                new { message = "Module is required." });
        }

        var workflowBuilders = await repository.GetByModuleAsync(
            module,
            cancellationToken);

        return Results.Ok(
            workflowBuilders.Select(WorkflowBuilderResponse.FromEntity));
    }

    private static async Task<IResult> CreateWorkflowBuilderAsync(
        CreateWorkflowBuilderRequest request,
        IWorkflowBuilderRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var workflowBuilder = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "WorkflowBuilder",
            workflowBuilder.Id,
            workflowBuilder.WorkflowName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/workflow-builders/{workflowBuilder.Id}",
            WorkflowBuilderResponse.FromEntity(workflowBuilder));
    }

    private static async Task<IResult> UpdateWorkflowBuilderAsync(
        string id,
        UpdateWorkflowBuilderRequest request,
        IWorkflowBuilderRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid workflow builder id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var workflowBuilder = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workflowBuilder is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(workflowBuilder);

        var updated = await repository.UpdateAsync(
            id,
            workflowBuilder,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "WorkflowBuilder",
            workflowBuilder.Id,
            workflowBuilder.WorkflowName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(WorkflowBuilderResponse.FromEntity(workflowBuilder));
    }

    private static async Task<IResult> DeleteWorkflowBuilderAsync(
        string id,
        IWorkflowBuilderRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid workflow builder id." });
        }

        var workflowBuilder = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workflowBuilder is null)
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
            "WorkflowBuilder",
            workflowBuilder.Id,
            workflowBuilder.WorkflowName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
