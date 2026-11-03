using WarehouseEntity = A360.MasterDataManagement.Domain.Entities.Warehouse;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class WarehouseEndpoints
{
    public static RouteGroupBuilder MapWarehouseEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/warehouses")
            .WithTags("Warehouses");

        group.MapGet("", GetWarehousesAsync)
            .WithName("GetWarehouses");

        group.MapGet("/{id}", GetWarehouseByIdAsync)
            .WithName("GetWarehouseById");

        group.MapPost("", CreateWarehouseAsync)
            .WithName("CreateWarehouse");

        group.MapPut("/{id}", UpdateWarehouseAsync)
            .WithName("UpdateWarehouse");

        group.MapPatch("/{id}/status", UpdateWarehouseStatusAsync)
            .WithName("UpdateWarehouseStatus");

        group.MapDelete("/{id}", DeleteWarehouseAsync)
            .WithName("DeleteWarehouse");

        return group;
    }

    private static async Task<IResult> GetWarehousesAsync(
        string? search,
        string? status,
        string? managerId,
        string? clientId,
        IWarehouseRepository repository,
        IStoreRepository stores,
        CancellationToken cancellationToken)
    {
        var warehouses = await repository.SearchAsync(
            new WarehouseFilter(search, status, managerId, clientId),
            cancellationToken);

        var storeCounts = await stores.CountByWarehouseAsync(
            warehouses.Select(x => x.Id),
            cancellationToken);

        return Results.Ok(
            warehouses.Select(x => WarehouseResponse.FromEntity(
                x,
                storeCounts.GetValueOrDefault(x.Id))));
    }

    private static async Task<IResult> GetWarehouseByIdAsync(
        string id,
        IWarehouseRepository repository,
        IStoreRepository stores,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid warehouse id." });
        }

        var warehouse = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (warehouse is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Warehouse",
            warehouse.Id,
            warehouse.WarehouseName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(await ToResponseAsync(warehouse, stores, cancellationToken));
    }

    private static async Task<IResult> CreateWarehouseAsync(
        CreateWarehouseRequest request,
        IWarehouseRepository repository,
        IEmployeeRepository employees,
        IStoreRepository stores,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var warehouse = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            warehouse.Code,
            warehouse.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A warehouse with code '{warehouse.Code}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            warehouse,
            employees,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        warehouse = await repository.CreateAsync(
            warehouse,
            cancellationToken);

        await eventLogger.LogAsync(
            "Warehouse",
            warehouse.Id,
            warehouse.WarehouseName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/warehouses/{warehouse.Id}",
            await ToResponseAsync(warehouse, stores, cancellationToken));
    }

    private static async Task<IResult> UpdateWarehouseAsync(
        string id,
        UpdateWarehouseRequest request,
        IWarehouseRepository repository,
        IEmployeeRepository employees,
        IStoreRepository stores,
        IBinRepository bins,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid warehouse id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var warehouse = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (warehouse is null)
        {
            return Results.NotFound();
        }

        var previousName = warehouse.WarehouseName;

        request.ApplyTo(warehouse);

        var existing = await repository.GetByCodeAsync(
            warehouse.Code,
            warehouse.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A warehouse with code '{warehouse.Code}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            warehouse,
            employees,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        var updated = await repository.UpdateAsync(
            id,
            warehouse,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (previousName != warehouse.WarehouseName)
        {
            await stores.UpdateWarehouseNameAsync(
                id,
                warehouse.WarehouseName,
                cancellationToken);

            await bins.UpdateWarehouseNameAsync(
                id,
                warehouse.WarehouseName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Warehouse",
            warehouse.Id,
            warehouse.WarehouseName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(await ToResponseAsync(warehouse, stores, cancellationToken));
    }

    private static async Task<IResult> UpdateWarehouseStatusAsync(
        string id,
        UpdateWarehouseStatusRequest request,
        IWarehouseRepository repository,
        IStoreRepository stores,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid warehouse id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var warehouse = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (warehouse is null)
        {
            return Results.NotFound();
        }

        warehouse.Status = request.Status;
        warehouse.UpdatedBy = request.UpdatedBy;
        warehouse.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            warehouse,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Warehouse",
            warehouse.Id,
            warehouse.WarehouseName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(await ToResponseAsync(warehouse, stores, cancellationToken));
    }

    private static async Task<IResult> DeleteWarehouseAsync(
        string id,
        IWarehouseRepository repository,
        IStoreRepository stores,
        IBinRepository bins,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid warehouse id." });
        }

        var warehouse = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (warehouse is null)
        {
            return Results.NotFound();
        }

        if (await stores.AnyWithWarehouseAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Stores still belong to this warehouse. Delete or move them first." });
        }

        if (await bins.AnyWithWarehouseAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Bins still belong to this warehouse. Delete or move them first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Warehouse",
            warehouse.Id,
            warehouse.WarehouseName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Checks every linked record exists and copies its current name across. Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveReferencesAsync(
        WarehouseEntity warehouse,
        IEmployeeRepository employees,
        CancellationToken cancellationToken)
    {
        var managerError = await MasterDataReferenceResolver.ResolveEmployeeAsync(
            warehouse.Manager,
            employees,
            cancellationToken);

        if (managerError is not null)
        {
            return managerError;
        }

        return null;
    }

    private static async Task<WarehouseResponse> ToResponseAsync(
        WarehouseEntity warehouse,
        IStoreRepository stores,
        CancellationToken cancellationToken)
    {
        var storeCounts = await stores.CountByWarehouseAsync(
            [warehouse.Id],
            cancellationToken);

        return WarehouseResponse.FromEntity(
            warehouse,
            storeCounts.GetValueOrDefault(warehouse.Id));
    }
}
