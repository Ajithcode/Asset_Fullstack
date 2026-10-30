
using A360.Workflows.Api.Contracts;
using A360.Workflows.Api.Validation;
using A360.Workflows.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Workflows.Api.Endpoints;

public static class WorkflowInsightEndpoints
{
    public static RouteGroupBuilder MapWorkflowInsightEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/workflow-insights")
            .WithTags("WorkflowInsights");

        group.MapGet("", GetWorkflowInsightsAsync)
            .WithName("GetWorkflowInsights");

        group.MapGet("/{id}", GetWorkflowInsightByIdAsync)
            .WithName("GetWorkflowInsightById");

        group.MapGet("/module/{module}", GetWorkflowInsightsByModuleAsync)
            .WithName("GetWorkflowInsightsByModule");

        group.MapPost("", CreateWorkflowInsightAsync)
            .WithName("CreateWorkflowInsight");

        group.MapPut("/{id}", UpdateWorkflowInsightAsync)
            .WithName("UpdateWorkflowInsight");

        group.MapDelete("/{id}", DeleteWorkflowInsightAsync)
            .WithName("DeleteWorkflowInsight");

        return group;
    }

    private static async Task<IResult> GetWorkflowInsightsAsync(
        IWorkflowInsightRepository repository,
        CancellationToken cancellationToken)
    {
        var workflowInsights = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            workflowInsights.Select(WorkflowInsightResponse.FromEntity));
    }

    private static async Task<IResult> GetWorkflowInsightByIdAsync(
        string id,
        IWorkflowInsightRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid workflow insight id." });
        }

        var workflowInsight = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workflowInsight is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "WorkflowInsight",
            workflowInsight.Id,
            workflowInsight.WorkflowName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(WorkflowInsightResponse.FromEntity(workflowInsight));
    }

    private static async Task<IResult> GetWorkflowInsightsByModuleAsync(
        string module,
        IWorkflowInsightRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            return Results.BadRequest(
                new { message = "Module is required." });
        }

        var workflowInsights = await repository.GetByModuleAsync(
            module,
            cancellationToken);

        return Results.Ok(
            workflowInsights.Select(WorkflowInsightResponse.FromEntity));
    }

    private static async Task<IResult> CreateWorkflowInsightAsync(
        CreateWorkflowInsightRequest request,
        IWorkflowInsightRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var workflowInsight = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "WorkflowInsight",
            workflowInsight.Id,
            workflowInsight.WorkflowName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/workflow-insights/{workflowInsight.Id}",
            WorkflowInsightResponse.FromEntity(workflowInsight));
    }

    private static async Task<IResult> UpdateWorkflowInsightAsync(
        string id,
        UpdateWorkflowInsightRequest request,
        IWorkflowInsightRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid workflow insight id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var workflowInsight = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workflowInsight is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(workflowInsight);

        var updated = await repository.UpdateAsync(
            id,
            workflowInsight,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "WorkflowInsight",
            workflowInsight.Id,
            workflowInsight.WorkflowName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(WorkflowInsightResponse.FromEntity(workflowInsight));
    }

    private static async Task<IResult> DeleteWorkflowInsightAsync(
        string id,
        IWorkflowInsightRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid workflow insight id." });
        }

        var workflowInsight = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workflowInsight is null)
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
            "WorkflowInsight",
            workflowInsight.Id,
            workflowInsight.WorkflowName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
