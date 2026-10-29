using AssetCategoryEntity = A360.MasterDataManagement.Domain.Entities.AssetCategory;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class AssetCategoryEndpoints
{
    // Guards the parent-chain walk against corrupt data that already contains a loop.
    private const int MaxHierarchyDepth = 100;

    public static RouteGroupBuilder MapAssetCategoryEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/asset-categories")
            .WithTags("AssetCategories");

        group.MapGet("", GetAssetCategoriesAsync)
            .WithName("GetAssetCategories");

        group.MapGet("/{id}", GetAssetCategoryByIdAsync)
            .WithName("GetAssetCategoryById");

        group.MapGet("/{id}/children", GetChildAssetCategoriesAsync)
            .WithName("GetChildAssetCategories");

        group.MapPost("", CreateAssetCategoryAsync)
            .WithName("CreateAssetCategory");

        group.MapPut("/{id}", UpdateAssetCategoryAsync)
            .WithName("UpdateAssetCategory");

        group.MapPatch("/{id}/status", UpdateAssetCategoryStatusAsync)
            .WithName("UpdateAssetCategoryStatus");

        group.MapDelete("/{id}", DeleteAssetCategoryAsync)
            .WithName("DeleteAssetCategory");

        return group;
    }

    private static async Task<IResult> GetAssetCategoriesAsync(
        string? search,
        string? status,
        string? assetType,
        string? parentCategoryId,
        string? asset,
        string? clientId,
        IAssetCategoryRepository repository,
        CancellationToken cancellationToken)
    {
        var categories = await repository.SearchAsync(
            new AssetCategoryFilter(search, status, assetType, parentCategoryId, asset, clientId),
            cancellationToken);

        return Results.Ok(
            categories.Select(AssetCategoryResponse.FromEntity));
    }

    private static async Task<IResult> GetAssetCategoryByIdAsync(
        string id,
        IAssetCategoryRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid asset category id." });
        }

        var category = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (category is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "AssetCategory",
            category.Id,
            category.CategoryName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(AssetCategoryResponse.FromEntity(category));
    }

    private static async Task<IResult> GetChildAssetCategoriesAsync(
        string id,
        IAssetCategoryRepository repository,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid asset category id." });
        }

        var children = await repository.GetChildrenAsync(
            id,
            cancellationToken);

        return Results.Ok(
            children.Select(AssetCategoryResponse.FromEntity));
    }

    private static async Task<IResult> CreateAssetCategoryAsync(
        CreateAssetCategoryRequest request,
        IAssetCategoryRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var category = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            category.Code,
            category.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"An asset category with code '{category.Code}' already exists." });
        }

        var parentError = await ResolveParentAsync(
            category,
            repository,
            cancellationToken);

        if (parentError is not null)
        {
            return parentError;
        }

        category = await repository.CreateAsync(
            category,
            cancellationToken);

        await eventLogger.LogAsync(
            "AssetCategory",
            category.Id,
            category.CategoryName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/asset-categories/{category.Id}",
            AssetCategoryResponse.FromEntity(category));
    }

    private static async Task<IResult> UpdateAssetCategoryAsync(
        string id,
        UpdateAssetCategoryRequest request,
        IAssetCategoryRepository repository,
        IAssetTypeRepository assetTypes,
        IAssetModelRepository assetModels,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid asset category id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var category = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (category is null)
        {
            return Results.NotFound();
        }

        var previousName = category.CategoryName;

        request.ApplyTo(category);

        var existing = await repository.GetByCodeAsync(
            category.Code,
            category.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"An asset category with code '{category.Code}' already exists." });
        }

        var parentError = await ResolveParentAsync(
            category,
            repository,
            cancellationToken);

        if (parentError is not null)
        {
            return parentError;
        }

        var updated = await repository.UpdateAsync(
            id,
            category,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        if (previousName != category.CategoryName)
        {
            await repository.UpdateParentCategoryNameAsync(
                id,
                category.CategoryName,
                cancellationToken);

            await assetTypes.UpdateCategoryNameAsync(
                id,
                category.CategoryName,
                cancellationToken);

            await assetModels.UpdateCategoryNameAsync(
                id,
                category.CategoryName,
                cancellationToken);
        }

        await eventLogger.LogAsync(
            "AssetCategory",
            category.Id,
            category.CategoryName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(AssetCategoryResponse.FromEntity(category));
    }

    private static async Task<IResult> UpdateAssetCategoryStatusAsync(
        string id,
        UpdateAssetCategoryStatusRequest request,
        IAssetCategoryRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid asset category id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var category = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (category is null)
        {
            return Results.NotFound();
        }

        category.Status = request.Status;
        category.UpdatedBy = request.UpdatedBy;
        category.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            category,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "AssetCategory",
            category.Id,
            category.CategoryName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(AssetCategoryResponse.FromEntity(category));
    }

    private static async Task<IResult> DeleteAssetCategoryAsync(
        string id,
        IAssetCategoryRepository repository,
        IAssetTypeRepository assetTypes,
        IAssetModelRepository assetModels,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid asset category id." });
        }

        var category = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (category is null)
        {
            return Results.NotFound();
        }

        if (await repository.HasChildrenAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "This asset category has sub-categories. Delete or move them first." });
        }

        if (await assetTypes.AnyWithCategoryAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Asset types still belong to this category. Delete or move them first." });
        }

        if (await assetModels.AnyWithCategoryAsync(id, cancellationToken))
        {
            return Results.Conflict(
                new { message = "Asset models still belong to this category. Delete or move them first." });
        }

        var deleted = await repository.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "AssetCategory",
            category.Id,
            category.CategoryName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Checks the parent category exists and is not this category or one of its sub-categories, and copies
    /// the parent's current name onto the reference. Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveParentAsync(
        AssetCategoryEntity category,
        IAssetCategoryRepository repository,
        CancellationToken cancellationToken)
    {
        var parentId = category.ParentCategory?.Id;

        if (parentId is null)
        {
            return null;
        }

        if (parentId == category.Id)
        {
            return Results.BadRequest(
                new { message = "An asset category cannot be its own parent." });
        }

        var parent = MongoObjectId.IsValid(parentId)
            ? await repository.GetByIdAsync(parentId, cancellationToken)
            : null;

        if (parent is null)
        {
            return Results.BadRequest(
                new { message = $"Asset category '{parentId}' was not found." });
        }

        category.ParentCategory!.Name = parent.CategoryName;

        // New categories have no id yet, so they cannot appear in any existing chain.
        if (!string.IsNullOrEmpty(category.Id))
        {
            var ancestor = parent;

            for (var depth = 0; ancestor?.ParentCategory?.Id is not null && depth < MaxHierarchyDepth; depth++)
            {
                if (ancestor.ParentCategory.Id == category.Id)
                {
                    return Results.BadRequest(
                        new { message = "Parent category cannot be one of this category's own sub-categories." });
                }

                ancestor = await repository.GetByIdAsync(
                    ancestor.ParentCategory.Id,
                    cancellationToken);
            }
        }

        return null;
    }
}
