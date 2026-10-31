
using A360.Workflows.Api.Contracts;
using A360.Workflows.Api.Validation;
using A360.Workflows.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Workflows.Api.Endpoints;

public static class WorkflowListEndpoints
{
    public static RouteGroupBuilder MapWorkflowListEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/workflow-lists")
            .WithTags("WorkflowLists");

        group.MapGet("", GetWorkflowListsAsync)
            .WithName("GetWorkflowLists");

        group.MapGet("/{id}", GetWorkflowListByIdAsync)
            .WithName("GetWorkflowListById");

        group.MapGet("/module/{module}", GetWorkflowListsByModuleAsync)
            .WithName("GetWorkflowListsByModule");

        group.MapPost("", CreateWorkflowListAsync)
            .WithName("CreateWorkflowList");

        group.MapPut("/{id}", UpdateWorkflowListAsync)
            .WithName("UpdateWorkflowList");

        group.MapDelete("/{id}", DeleteWorkflowListAsync)
            .WithName("DeleteWorkflowList");

        return group;
    }

    private static async Task<IResult> GetWorkflowListsAsync(
        IWorkflowListRepository repository,
        CancellationToken cancellationToken)
    {
        var workflowLists = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            workflowLists.Select(WorkflowListResponse.FromEntity));
    }

    private static async Task<IResult> GetWorkflowListByIdAsync(
        string id,
        IWorkflowListRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid workflow id." });
        }

        var workflowList = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workflowList is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "WorkflowList",
            workflowList.Id,
            workflowList.WorkflowName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(WorkflowListResponse.FromEntity(workflowList));
    }

    private static async Task<IResult> GetWorkflowListsByModuleAsync(
        string module,
        IWorkflowListRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            return Results.BadRequest(
                new { message = "Module is required." });
        }

        var workflowLists = await repository.GetByModuleAsync(
            module,
            cancellationToken);

        return Results.Ok(
            workflowLists.Select(WorkflowListResponse.FromEntity));
    }

    private static async Task<IResult> CreateWorkflowListAsync(
        CreateWorkflowListRequest request,
        IWorkflowListRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var workflowList = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "WorkflowList",
            workflowList.Id,
            workflowList.WorkflowName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/workflow-lists/{workflowList.Id}",
            WorkflowListResponse.FromEntity(workflowList));
    }

    private static async Task<IResult> UpdateWorkflowListAsync(
        string id,
        UpdateWorkflowListRequest request,
        IWorkflowListRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid workflow id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var workflowList = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workflowList is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(workflowList);

        var updated = await repository.UpdateAsync(
            id,
            workflowList,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "WorkflowList",
            workflowList.Id,
            workflowList.WorkflowName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(WorkflowListResponse.FromEntity(workflowList));
    }

    private static async Task<IResult> DeleteWorkflowListAsync(
        string id,
        IWorkflowListRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid workflow id." });
        }

        var workflowList = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workflowList is null)
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
            "WorkflowList",
            workflowList.Id,
            workflowList.WorkflowName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
