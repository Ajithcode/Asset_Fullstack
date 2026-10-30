using BinEntity = A360.MasterDataManagement.Domain.Entities.Bin;
using A360.MasterDataManagement.Domain.ValueObjects;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class BinEndpoints
{
    public static RouteGroupBuilder MapBinEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/bins")
            .WithTags("Bins");

        group.MapGet("", GetBinsAsync)
            .WithName("GetBins");

        group.MapGet("/{id}", GetBinByIdAsync)
            .WithName("GetBinById");

        group.MapPost("", CreateBinAsync)
            .WithName("CreateBin");

        group.MapPut("/{id}", UpdateBinAsync)
            .WithName("UpdateBin");

        group.MapPatch("/{id}/status", UpdateBinStatusAsync)
            .WithName("UpdateBinStatus");

        group.MapDelete("/{id}", DeleteBinAsync)
            .WithName("DeleteBin");

        return group;
    }

    private static async Task<IResult> GetBinsAsync(
        string? search,
        string? status,
        string? storeId,
        string? warehouseId,
        string? clientId,
        IBinRepository repository,
        CancellationToken cancellationToken)
    {
        var bins = await repository.SearchAsync(
            new BinFilter(search, status, storeId, warehouseId, clientId),
            cancellationToken);

        return Results.Ok(
            bins.Select(BinResponse.FromEntity));
    }

    private static async Task<IResult> GetBinByIdAsync(
        string id,
        IBinRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid bin id." });
        }

        var bin = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (bin is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Bin",
            bin.Id,
            bin.BinName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(BinResponse.FromEntity(bin));
    }

    private static async Task<IResult> CreateBinAsync(
        CreateBinRequest request,
        IBinRepository repository,
        IStoreRepository stores,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var bin = request.ToEntity();

        var referenceError = await ResolveReferencesAsync(
            bin,
            stores,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        var existing = await repository.GetByCodeAsync(
            bin.Code,
            bin.Store!.Id!,
            bin.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"A bin with code '{bin.Code}' already exists in store '{bin.Store!.Name}'." });
        }

        bin = await repository.CreateAsync(
            bin,
            cancellationToken);

        await eventLogger.LogAsync(
            "Bin",
            bin.Id,
            bin.BinName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/bins/{bin.Id}",
            BinResponse.FromEntity(bin));
    }

    private static async Task<IResult> UpdateBinAsync(
        string id,
        UpdateBinRequest request,
        IBinRepository repository,
        IStoreRepository stores,
        IItemRepository items,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid bin id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var bin = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (bin is null)
        {
            return Results.NotFound();
        }

        var previousStoreId = bin.Store?.Id;
        var previousWarehouseId = bin.Warehouse?.Id;

        request.ApplyTo(bin);

        var referenceError = await ResolveReferencesAsync(
            bin,
            stores,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        var existing = await repository.GetByCodeAsync(
            bin.Code,
            bin.Store!.Id!,
            bin.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"A bin with code '{bin.Code}' already exists in store '{bin.Store!.Name}'." });
        }

        var updated = await repository.UpdateAsync(
            id,
            bin,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        // Items kept in this bin move with it.
        if (previousStoreId != bin.Store?.Id || previousWarehouseId != bin.Warehouse?.Id)
        {
            await items.SetStoreForBinAsync(
                id,
                bin.Store,
                bin.Warehouse,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "Bin",
            bin.Id,
            bin.BinName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(BinResponse.FromEntity(bin));
    }

    private static async Task<IResult> UpdateBinStatusAsync(
        string id,
        UpdateBinStatusRequest request,
        IBinRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid bin id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var bin = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (bin is null)
        {
            return Results.NotFound();
        }

        bin.Status = request.Status;
        bin.UpdatedBy = request.UpdatedBy;
        bin.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            bin,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Bin",
            bin.Id,
            bin.BinName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(BinResponse.FromEntity(bin));
    }

    private static async Task<IResult> DeleteBinAsync(
        string id,
        IBinRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid bin id." });
        }

        var bin = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (bin is null)
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
            "Bin",
            bin.Id,
            bin.BinName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Checks every linked record exists and copies its current name across. Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveReferencesAsync(
        BinEntity bin,
        IStoreRepository stores,
        CancellationToken cancellationToken)
    {
        var storeError = await MasterDataReferenceResolver.ResolveStoreAsync(
            bin.Store,
            stores,
            cancellationToken);

        if (storeError is not null)
        {
            return storeError;
        }

        // The warehouse always follows the store.
        var store = await stores.GetByIdAsync(
            bin.Store!.Id!,
            cancellationToken);

        bin.Warehouse = store?.Warehouse is null
            ? null
            : new MasterDataReference { Id = store.Warehouse.Id, Name = store.Warehouse.Name };

        return null;
    }
}
