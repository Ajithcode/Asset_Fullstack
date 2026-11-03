using ItemCategoryEntity = A360.MasterDataManagement.Domain.Entities.ItemCategory;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class ItemCategoryEndpoints
{
    // Guards the parent-chain walk against corrupt data that already contains a loop.
    private const int MaxHierarchyDepth = 100;

    public static RouteGroupBuilder MapItemCategoryEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/item-categories")
            .WithTags("ItemCategories");

        group.MapGet("", GetItemCategoriesAsync)
            .WithName("GetItemCategories");

        group.MapGet("/{id}", GetItemCategoryByIdAsync)
            .WithName("GetItemCategoryById");

        group.MapGet("/{id}/children", GetChildItemCategoriesAsync)
            .WithName("GetChildItemCategories");

        group.MapPost("", CreateItemCategoryAsync)
            .WithName("CreateItemCategory");

        group.MapPut("/{id}", UpdateItemCategoryAsync)
            .WithName("UpdateItemCategory");

        group.MapPatch("/{id}/status", UpdateItemCategoryStatusAsync)
            .WithName("UpdateItemCategoryStatus");

        group.MapDelete("/{id}", DeleteItemCategoryAsync)
            .WithName("DeleteItemCategory");

        return group;
    }

    private static async Task<IResult> GetItemCategoriesAsync(
        string? search,
        string? status,
        string? parentCategoryId,
        string? clientId,
        IItemCategoryRepository repository,
        CancellationToken cancellationToken)
    {
        var itemCategories = await repository.SearchAsync(
            new ItemCategoryFilter(search, status, parentCategoryId, clientId),
            cancellationToken);

        return Results.Ok(
            itemCategories.Select(ItemCategoryResponse.FromEntity));
    }

    private static async Task<IResult> GetItemCategoryByIdAsync(
        string id,
        IItemCategoryRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid item category id." });
        }

        var itemCategory = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (itemCategory is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ItemCategory",
            itemCategory.Id,
            itemCategory.CategoryName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(ItemCategoryResponse.FromEntity(itemCategory));
    }

    private static async Task<IResult> GetChildItemCategoriesAsync(
        string id,
        IItemCategoryRepository repository,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid item category id." });
        }

        var children = await repository.GetChildrenAsync(
            id,
            cancellationToken);

        return Results.Ok(
            children.Select(ItemCategoryResponse.FromEntity));
    }

    private static async Task<IResult> CreateItemCategoryAsync(
        CreateItemCategoryRequest request,
        IItemCategoryRepository repository,
        IUomRepository uoms,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var itemCategory = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            itemCategory.Code,
            itemCategory.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"An item category with code '{itemCategory.Code}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            itemCategory,
            repository,
            uoms,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        itemCategory = await repository.CreateAsync(
            itemCategory,
            cancellationToken);

        await eventLogger.LogAsync(
            "ItemCategory",
            itemCategory.Id,
            itemCategory.CategoryName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/item-categories/{itemCategory.Id}",
            ItemCategoryResponse.FromEntity(itemCategory));
    }

    private static async Task<IResult> UpdateItemCategoryAsync(
        string id,
        UpdateItemCategoryRequest request,
        IItemCategoryRepository repository,
        IUomRepository uoms,
        IItemRepository items,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid item category id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var itemCategory = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (itemCategory is null)
        {
            return Results.NotFound();
        }

        var previousName = itemCategory.CategoryName;

        request.ApplyTo(itemCategory);

        var existing = await repository.GetByCodeAsync(
            itemCategory.Code,
            itemCategory.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"An item category with code '{itemCategory.Code}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            itemCategory,
            repository,
            uoms,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        var updated = await repository.UpdateAsync(
            id,
            itemCategory,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (previousName != itemCategory.CategoryName)
        {
            await repository.UpdateParentCategoryNameAsync(
                id,
                itemCategory.CategoryName,
                cancellationToken);

            await items.UpdateCategoryNameAsync(
                id,
                itemCategory.CategoryName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "ItemCategory",
            itemCategory.Id,
            itemCategory.CategoryName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ItemCategoryResponse.FromEntity(itemCategory));
    }

    private static async Task<IResult> UpdateItemCategoryStatusAsync(
        string id,
        UpdateItemCategoryStatusRequest request,
        IItemCategoryRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid item category id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var itemCategory = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (itemCategory is null)
        {
            return Results.NotFound();
        }

        itemCategory.Status = request.Status;
        itemCategory.UpdatedBy = request.UpdatedBy;
        itemCategory.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            itemCategory,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ItemCategory",
            itemCategory.Id,
            itemCategory.CategoryName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(ItemCategoryResponse.FromEntity(itemCategory));
    }

    private static async Task<IResult> DeleteItemCategoryAsync(
        string id,
        IItemCategoryRepository repository,
        IItemRepository items,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid item category id." });
        }

        var itemCategory = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (itemCategory is null)
        {
            return Results.NotFound();
        }

        if (await repository.HasChildrenAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "This item category has sub-categories. Delete or move them first." });
        }

        if (await items.AnyWithCategoryAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Items still use this category. Change or delete them first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "ItemCategory",
            itemCategory.Id,
            itemCategory.CategoryName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Checks the default unit exists, and the parent category exists and is not this category or one of its sub-categories, copying the parent's current name across. Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveReferencesAsync(
        ItemCategoryEntity itemCategory,
        IItemCategoryRepository repository,
        IUomRepository uoms,
        CancellationToken cancellationToken)
    {
        var uomError = await MasterDataReferenceResolver.ResolveUomAsync(
            itemCategory.DefaultUom,
            uoms,
            cancellationToken);

        if (uomError is not null)
        {
            return uomError;
        }

        var parentId = itemCategory.ParentCategory?.Id;

        if (parentId is not null)
        {
            if (parentId == itemCategory.Id)
            {
                return Results.BadRequest(
                    new { message = "An item category cannot be its own parent." });
            }

            var parent = MongoObjectId.IsValid(parentId)
                ? await repository.GetByIdAsync(parentId, cancellationToken)
                : null;

            if (parent is null)
            {
                return Results.BadRequest(
                    new { message = $"Item category '{parentId}' was not found." });
            }

            itemCategory.ParentCategory!.Name = parent.CategoryName;

            // New records have no id yet, so they cannot appear in any existing chain.
            if (!string.IsNullOrEmpty(itemCategory.Id))
            {
                var ancestor = parent;

                for (var depth = 0; ancestor?.ParentCategory?.Id is not null && depth < MaxHierarchyDepth; depth++)
                {
                    if (ancestor.ParentCategory.Id == itemCategory.Id)
                    {
                        return Results.BadRequest(
                            new { message = "The parent cannot be one of this item category's own sub-categories." });
                    }

                    ancestor = await repository.GetByIdAsync(
                        ancestor.ParentCategory.Id,
                        cancellationToken);
                }
            }
        }

        return null;
    }
}
