using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class StockStatusEndpoints
{
    public static RouteGroupBuilder MapStockStatusEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/stock-statuses")
            .WithTags("StockStatuses");

        group.MapGet("", GetStockStatusesAsync)
            .WithName("GetStockStatuses");

        group.MapGet("/{id}", GetStockStatusByIdAsync)
            .WithName("GetStockStatusById");

        group.MapPost("", CreateStockStatusAsync)
            .WithName("CreateStockStatus");

        group.MapPut("/{id}", UpdateStockStatusAsync)
            .WithName("UpdateStockStatus");

        group.MapPatch("/{id}/status", UpdateStockStatusStatusAsync)
            .WithName("UpdateStockStatusStatus");

        group.MapDelete("/{id}", DeleteStockStatusAsync)
            .WithName("DeleteStockStatus");

        return group;
    }

    private static async Task<IResult> GetStockStatusesAsync(
        string? search,
        string? status,
        string? type,
        bool? systemDefault,
        string? clientId,
        IStockStatusRepository repository,
        CancellationToken cancellationToken)
    {
        var stockStatuses = await repository.SearchAsync(
            new StockStatusFilter(search, status, type, systemDefault, clientId),
            cancellationToken);

        return Results.Ok(
            stockStatuses.Select(StockStatusResponse.FromEntity));
    }

    private static async Task<IResult> GetStockStatusByIdAsync(
        string id,
        IStockStatusRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid stock status id." });
        }

        var stockStatus = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (stockStatus is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "StockStatus",
            stockStatus.Id,
            stockStatus.StatusName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(StockStatusResponse.FromEntity(stockStatus));
    }

    private static async Task<IResult> CreateStockStatusAsync(
        CreateStockStatusRequest request,
        IStockStatusRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var stockStatus = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            stockStatus.Code,
            stockStatus.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A stock status with code '{stockStatus.Code}' already exists." });
        }

        stockStatus = await repository.CreateAsync(
            stockStatus,
            cancellationToken);

        if (stockStatus.SystemDefault)
        {
            await repository.ClearSystemDefaultAsync(
                stockStatus.ClientId,
                stockStatus.Id,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "StockStatus",
            stockStatus.Id,
            stockStatus.StatusName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/stock-statuses/{stockStatus.Id}",
            StockStatusResponse.FromEntity(stockStatus));
    }

    private static async Task<IResult> UpdateStockStatusAsync(
        string id,
        UpdateStockStatusRequest request,
        IStockStatusRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid stock status id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var stockStatus = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (stockStatus is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(stockStatus);

        var existing = await repository.GetByCodeAsync(
            stockStatus.Code,
            stockStatus.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A stock status with code '{stockStatus.Code}' already exists." });
        }

        var updated = await repository.UpdateAsync(
            id,
            stockStatus,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (stockStatus.SystemDefault)
        {
            await repository.ClearSystemDefaultAsync(
                stockStatus.ClientId,
                stockStatus.Id,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "StockStatus",
            stockStatus.Id,
            stockStatus.StatusName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(StockStatusResponse.FromEntity(stockStatus));
    }

    private static async Task<IResult> UpdateStockStatusStatusAsync(
        string id,
        UpdateStockStatusStatusRequest request,
        IStockStatusRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid stock status id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var stockStatus = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (stockStatus is null)
        {
            return Results.NotFound();
        }

        stockStatus.Status = request.Status;
        stockStatus.UpdatedBy = request.UpdatedBy;
        stockStatus.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            stockStatus,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "StockStatus",
            stockStatus.Id,
            stockStatus.StatusName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(StockStatusResponse.FromEntity(stockStatus));
    }

    private static async Task<IResult> DeleteStockStatusAsync(
        string id,
        IStockStatusRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid stock status id." });
        }

        var stockStatus = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (stockStatus is null)
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
            "StockStatus",
            stockStatus.Id,
            stockStatus.StatusName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }
}
