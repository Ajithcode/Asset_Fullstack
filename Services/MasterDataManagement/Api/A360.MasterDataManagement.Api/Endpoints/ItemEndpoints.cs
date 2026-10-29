using ItemEntity = A360.MasterDataManagement.Domain.Entities.Item;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class ItemEndpoints
{
    public static RouteGroupBuilder MapItemEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/items")
            .WithTags("Items");

        group.MapGet("", GetItemsAsync)
            .WithName("GetItems");

        group.MapGet("/{id}", GetItemByIdAsync)
            .WithName("GetItemById");

        group.MapPost("", CreateItemAsync)
            .WithName("CreateItem");

        group.MapPut("/{id}", UpdateItemAsync)
            .WithName("UpdateItem");

        group.MapPatch("/{id}/status", UpdateItemStatusAsync)
            .WithName("UpdateItemStatus");

        group.MapDelete("/{id}", DeleteItemAsync)
            .WithName("DeleteItem");

        return group;
    }

    private static async Task<IResult> GetItemsAsync(
        string? search,
        string? status,
        string? categoryId,
        string? uomId,
        string? clientId,
        IItemRepository repository,
        CancellationToken cancellationToken)
    {
        var items = await repository.SearchAsync(
            new ItemFilter(search, status, categoryId, uomId, clientId),
            cancellationToken);

        return Results.Ok(
            items.Select(ItemResponse.FromEntity));
    }

    private static async Task<IResult> GetItemByIdAsync(
        string id,
        IItemRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid item id." });
        }

        var item = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (item is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Item",
            item.Id,
            item.ItemName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(ItemResponse.FromEntity(item));
    }

    private static async Task<IResult> CreateItemAsync(
        CreateItemRequest request,
        IItemRepository repository,
        IItemCategoryRepository itemCategories,
        IUomRepository uoms,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var item = request.ToEntity();

        var existing = await repository.GetByItemCodeAsync(
            item.ItemCode,
            item.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"An item with code '{item.ItemCode}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            item,
            itemCategories,
            uoms,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        item = await repository.CreateAsync(
            item,
            cancellationToken);

        await eventLogger.LogAsync(
            "Item",
            item.Id,
            item.ItemName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/items/{item.Id}",
            ItemResponse.FromEntity(item));
    }

    private static async Task<IResult> UpdateItemAsync(
        string id,
        UpdateItemRequest request,
        IItemRepository repository,
        IItemCategoryRepository itemCategories,
        IUomRepository uoms,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid item id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var item = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (item is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(item);

        var existing = await repository.GetByItemCodeAsync(
            item.ItemCode,
            item.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"An item with code '{item.ItemCode}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            item,
            itemCategories,
            uoms,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        var updated = await repository.UpdateAsync(
            id,
            item,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Item",
            item.Id,
            item.ItemName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ItemResponse.FromEntity(item));
    }

    private static async Task<IResult> UpdateItemStatusAsync(
        string id,
        UpdateItemStatusRequest request,
        IItemRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid item id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var item = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (item is null)
        {
            return Results.NotFound();
        }

        item.Status = request.Status;
        item.UpdatedBy = request.UpdatedBy;
        item.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            item,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "Item",
            item.Id,
            item.ItemName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ItemResponse.FromEntity(item));
    }

    private static async Task<IResult> DeleteItemAsync(
        string id,
        IItemRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid item id." });
        }

        var item = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (item is null)
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
            "Item",
            item.Id,
            item.ItemName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Checks every linked record exists and copies its current name across. Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveReferencesAsync(
        ItemEntity item,
        IItemCategoryRepository itemCategories,
        IUomRepository uoms,
        CancellationToken cancellationToken)
    {
        var categoryError = await MasterDataReferenceResolver.ResolveItemCategoryAsync(
            item.Category,
            itemCategories,
            cancellationToken);

        if (categoryError is not null)
        {
            return categoryError;
        }

        var uomError = await MasterDataReferenceResolver.ResolveUomAsync(
            item.Uom,
            uoms,
            cancellationToken);

        if (uomError is not null)
        {
            return uomError;
        }

        return null;
    }
}
