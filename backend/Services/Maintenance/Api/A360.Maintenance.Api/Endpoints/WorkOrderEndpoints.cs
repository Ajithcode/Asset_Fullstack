
using A360.Maintenance.Api.Contracts;
using A360.Maintenance.Api.Validation;
using A360.Maintenance.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.Maintenance.Api.Endpoints;

public static class WorkOrderEndpoints
{
    public static RouteGroupBuilder MapWorkOrderEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/work-orders")
            .WithTags("WorkOrders");

        group.MapGet("", GetWorkOrdersAsync)
            .WithName("GetWorkOrders");

        group.MapGet("/{id}", GetWorkOrderByIdAsync)
            .WithName("GetWorkOrderById");

        group.MapGet("/asset/{assetId}", GetWorkOrdersByAssetIdAsync)
            .WithName("GetWorkOrdersByAssetId");

        group.MapPost("", CreateWorkOrderAsync)
            .WithName("CreateWorkOrder");

        group.MapPut("/{id}", UpdateWorkOrderAsync)
            .WithName("UpdateWorkOrder");

        group.MapDelete("/{id}", DeleteWorkOrderAsync)
            .WithName("DeleteWorkOrder");

        return group;
    }

    private static async Task<IResult> GetWorkOrdersAsync(
        IWorkOrderRepository repository,
        CancellationToken cancellationToken)
    {
        var workOrders = await repository.GetAllAsync(
            cancellationToken);

        return Results.Ok(
            workOrders.Select(WorkOrderResponse.FromEntity));
    }

    private static async Task<IResult> GetWorkOrderByIdAsync(
        string id,
        IWorkOrderRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid work order id." });
        }

        var workOrder = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workOrder is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "WorkOrder",
            workOrder.Id,
            workOrder.WorkOrderId,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(WorkOrderResponse.FromEntity(workOrder));
    }

    private static async Task<IResult> GetWorkOrdersByAssetIdAsync(
        string assetId,
        IWorkOrderRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return Results.BadRequest(
                new { message = "Asset id is required." });
        }

        var workOrders = await repository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        return Results.Ok(
            workOrders.Select(WorkOrderResponse.FromEntity));
    }

    private static async Task<IResult> CreateWorkOrderAsync(
        CreateWorkOrderRequest request,
        IWorkOrderRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var workOrder = await repository.CreateAsync(
            request.ToEntity(),
            cancellationToken);

        await eventLogger.LogAsync(
            "WorkOrder",
            workOrder.Id,
            workOrder.WorkOrderId,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/work-orders/{workOrder.Id}",
            WorkOrderResponse.FromEntity(workOrder));
    }

    private static async Task<IResult> UpdateWorkOrderAsync(
        string id,
        UpdateWorkOrderRequest request,
        IWorkOrderRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid work order id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var workOrder = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workOrder is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(workOrder);

        var updated = await repository.UpdateAsync(
            id,
            workOrder,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "WorkOrder",
            workOrder.Id,
            workOrder.WorkOrderId,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(WorkOrderResponse.FromEntity(workOrder));
    }

    private static async Task<IResult> DeleteWorkOrderAsync(
        string id,
        IWorkOrderRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid work order id." });
        }

        var workOrder = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (workOrder is null)
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
            "WorkOrder",
            workOrder.Id,
            workOrder.WorkOrderId,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
