using StoreEntity = A360.MasterDataManagement.Domain.Entities.Store;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class StoreEndpoints
{
    public static RouteGroupBuilder MapStoreEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/stores")
            .WithTags("Stores");

        group.MapGet("", GetStoresAsync)
            .WithName("GetStores");

        group.MapGet("/{id}", GetStoreByIdAsync)
            .WithName("GetStoreById");

        group.MapPost("", CreateStoreAsync)
            .WithName("CreateStore");

        group.MapPut("/{id}", UpdateStoreAsync)
            .WithName("UpdateStore");

        group.MapPatch("/{id}/status", UpdateStoreStatusAsync)
            .WithName("UpdateStoreStatus");

        group.MapDelete("/{id}", DeleteStoreAsync)
            .WithName("DeleteStore");

        return group;
    }

    private static async Task<IResult> GetStoresAsync(
        string? search,
        string? status,
        string? warehouseId,
        string? clientId,
        IStoreRepository repository,
        CancellationToken cancellationToken)
    {
        var stores = await repository.SearchAsync(
            new StoreFilter(search, status, warehouseId, clientId),
            cancellationToken);

        return Results.Ok(
            stores.Select(StoreResponse.FromEntity));
    }

    private static async Task<IResult> GetStoreByIdAsync(
        string id,
        IStoreRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid store id." });
        }

        var store = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (store is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Store",
            store.Id,
            store.StoreName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(StoreResponse.FromEntity(store));
    }

    private static async Task<IResult> CreateStoreAsync(
        CreateStoreRequest request,
        IStoreRepository repository,
        IWarehouseRepository warehouses,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var store = request.ToEntity();

        var referenceError = await ResolveReferencesAsync(
            store,
            warehouses,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        var existing = await repository.GetByCodeAsync(
            store.Code,
            store.Warehouse!.Id!,
            store.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A store with code '{store.Code}' already exists in warehouse '{store.Warehouse!.Name}'." });
        }

        store = await repository.CreateAsync(
            store,
            cancellationToken);

        await eventLogger.LogAsync(
            "Store",
            store.Id,
            store.StoreName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/stores/{store.Id}",
            StoreResponse.FromEntity(store));
    }

    private static async Task<IResult> UpdateStoreAsync(
        string id,
        UpdateStoreRequest request,
        IStoreRepository repository,
        IWarehouseRepository warehouses,
        IBinRepository bins,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid store id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var store = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (store is null)
        {
            return Results.NotFound();
        }

        var previousName = store.StoreName;
        var previousWarehouseId = store.Warehouse?.Id;

        request.ApplyTo(store);

        var referenceError = await ResolveReferencesAsync(
            store,
            warehouses,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        var existing = await repository.GetByCodeAsync(
            store.Code,
            store.Warehouse!.Id!,
            store.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A store with code '{store.Code}' already exists in warehouse '{store.Warehouse!.Name}'." });
        }

        var updated = await repository.UpdateAsync(
            id,
            store,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (previousName != store.StoreName)
        {
            await bins.UpdateStoreNameAsync(
                id,
                store.StoreName,
                cancellationToken);
        }

        // Records under this store move with it.
        if (previousWarehouseId != store.Warehouse?.Id)
        {
            await bins.SetWarehouseForStoreAsync(
                id,
                store.Warehouse,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Store",
            store.Id,
            store.StoreName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(StoreResponse.FromEntity(store));
    }

    private static async Task<IResult> UpdateStoreStatusAsync(
        string id,
        UpdateStoreStatusRequest request,
        IStoreRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid store id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var store = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (store is null)
        {
            return Results.NotFound();
        }

        store.Status = request.Status;
        store.UpdatedBy = request.UpdatedBy;
        store.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            store,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Store",
            store.Id,
            store.StoreName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(StoreResponse.FromEntity(store));
    }

    private static async Task<IResult> DeleteStoreAsync(
        string id,
        IStoreRepository repository,
        IBinRepository bins,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid store id." });
        }

        var store = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (store is null)
        {
            return Results.NotFound();
        }

        if (await bins.AnyWithStoreAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Bins still belong to this store. Delete or move them first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Store",
            store.Id,
            store.StoreName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Checks every linked record exists and copies its current name across. Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveReferencesAsync(
        StoreEntity store,
        IWarehouseRepository warehouses,
        CancellationToken cancellationToken)
    {
        var warehouseError = await MasterDataReferenceResolver.ResolveWarehouseAsync(
            store.Warehouse,
            warehouses,
            cancellationToken);

        if (warehouseError is not null)
        {
            return warehouseError;
        }

        return null;
    }
}
