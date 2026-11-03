using AssetSubCategoryEntity = A360.MasterDataManagement.Domain.Entities.AssetSubCategory;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class AssetSubCategoryEndpoints
{
    public static RouteGroupBuilder MapAssetSubCategoryEndpoints(
        this IEndpointRouteBuilder routes)
    {
        // Sub-categories of one category: GET /api/master-data/asset-categories/{categoryId}/subcategories
        routes.MapGet("/api/master-data/asset-categories/{categoryId}/subcategories", GetSubCategoriesByCategoryAsync)
            .WithTags("AssetSubCategories")
            .WithName("GetAssetSubCategoriesByCategory");

        var group = routes.MapGroup("/api/master-data/asset-subcategories")
            .WithTags("AssetSubCategories");

        group.MapGet("", GetAssetSubCategoriesAsync)
            .WithName("GetAssetSubCategories");

        group.MapGet("/{subCategoryId}", GetAssetSubCategoryByIdAsync)
            .WithName("GetAssetSubCategoryById");

        group.MapPost("", CreateAssetSubCategoryAsync)
            .WithName("CreateAssetSubCategory");

        group.MapPut("/{subCategoryId}", UpdateAssetSubCategoryAsync)
            .WithName("UpdateAssetSubCategory");

        group.MapPatch("/{subCategoryId}/status", UpdateAssetSubCategoryStatusAsync)
            .WithName("UpdateAssetSubCategoryStatus");

        group.MapDelete("/{subCategoryId}", DeleteAssetSubCategoryAsync)
            .WithName("DeleteAssetSubCategory");

        return group;
    }

    private static async Task<IResult> GetAssetSubCategoriesAsync(
        string? search,
        string? status,
        string? assetType,
        string? categoryId,
        string? asset,
        string? clientId,
        IAssetSubCategoryRepository repository,
        CancellationToken cancellationToken)
    {
        var subCategories = await repository.SearchAsync(
            new AssetSubCategoryFilter(search, status, assetType, categoryId, asset, clientId),
            cancellationToken);

        return Results.Ok(
            subCategories.Select(AssetSubCategoryResponse.FromEntity));
    }

    private static async Task<IResult> GetSubCategoriesByCategoryAsync(
        string categoryId,
        string? search,
        string? status,
        IAssetSubCategoryRepository repository,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(categoryId))
        {
            return Results.BadRequest(
                new { message = "Invalid asset category id." });
        }

        var subCategories = await repository.SearchAsync(
            new AssetSubCategoryFilter(search, status, null, categoryId, null, null),
            cancellationToken);

        return Results.Ok(
            subCategories.Select(AssetSubCategoryResponse.FromEntity));
    }

    private static async Task<IResult> GetAssetSubCategoryByIdAsync(
        string subCategoryId,
        IAssetSubCategoryRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(subCategoryId))
        {
            return Results.BadRequest(
                new { message = "Invalid asset sub-category id." });
        }

        var subCategory = await repository.GetByIdAsync(
            subCategoryId,
            cancellationToken);

        if (subCategory is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "AssetSubCategory",
            subCategory.Id,
            subCategory.SubCategoryName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(AssetSubCategoryResponse.FromEntity(subCategory));
    }

    private static async Task<IResult> CreateAssetSubCategoryAsync(
        CreateAssetSubCategoryRequest request,
        IAssetSubCategoryRepository repository,
        IAssetCategoryRepository categories,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var subCategory = request.ToEntity();

        var existing = await repository.GetByCodeAsync(
            subCategory.Code,
            subCategory.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"An asset sub-category with code '{subCategory.Code}' already exists." });
        }

        var categoryError = await ResolveCategoryAsync(
            subCategory,
            categories,
            cancellationToken);

        if (categoryError is not null)
        {
            return categoryError;
        }

        subCategory = await repository.CreateAsync(
            subCategory,
            cancellationToken);

        await eventLogger.LogAsync(
            "AssetSubCategory",
            subCategory.Id,
            subCategory.SubCategoryName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/asset-subcategories/{subCategory.Id}",
            AssetSubCategoryResponse.FromEntity(subCategory));
    }

    private static async Task<IResult> UpdateAssetSubCategoryAsync(
        string subCategoryId,
        UpdateAssetSubCategoryRequest request,
        IAssetSubCategoryRepository repository,
        IAssetCategoryRepository categories,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(subCategoryId))
        {
            return Results.BadRequest(
                new { message = "Invalid asset sub-category id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var subCategory = await repository.GetByIdAsync(
            subCategoryId,
            cancellationToken);

        if (subCategory is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(subCategory);

        var existing = await repository.GetByCodeAsync(
            subCategory.Code,
            subCategory.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != subCategoryId)
        {
            return Results.Conflict(
                new { message = $"An asset sub-category with code '{subCategory.Code}' already exists." });
        }

        var categoryError = await ResolveCategoryAsync(
            subCategory,
            categories,
            cancellationToken);

        if (categoryError is not null)
        {
            return categoryError;
        }

        var updated = await repository.UpdateAsync(
            subCategoryId,
            subCategory,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "AssetSubCategory",
            subCategory.Id,
            subCategory.SubCategoryName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(AssetSubCategoryResponse.FromEntity(subCategory));
    }

    private static async Task<IResult> UpdateAssetSubCategoryStatusAsync(
        string subCategoryId,
        UpdateAssetSubCategoryStatusRequest request,
        IAssetSubCategoryRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(subCategoryId))
        {
            return Results.BadRequest(
                new { message = "Invalid asset sub-category id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var subCategory = await repository.GetByIdAsync(
            subCategoryId,
            cancellationToken);

        if (subCategory is null)
        {
            return Results.NotFound();
        }

        subCategory.Status = request.Status;
        subCategory.UpdatedBy = request.UpdatedBy;
        subCategory.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            subCategoryId,
            subCategory,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "AssetSubCategory",
            subCategory.Id,
            subCategory.SubCategoryName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(AssetSubCategoryResponse.FromEntity(subCategory));
    }

    private static async Task<IResult> DeleteAssetSubCategoryAsync(
        string subCategoryId,
        IAssetSubCategoryRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(subCategoryId))
        {
            return Results.BadRequest(
                new { message = "Invalid asset sub-category id." });
        }

        var subCategory = await repository.GetByIdAsync(
            subCategoryId,
            cancellationToken);

        if (subCategory is null)
        {
            return Results.NotFound();
        }

        var deleted = await repository.DeleteAsync(
            subCategoryId,
            cancellationToken);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "AssetSubCategory",
            subCategory.Id,
            subCategory.SubCategoryName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Checks the category exists and copies its current name onto the sub-category.
    /// Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveCategoryAsync(
        AssetSubCategoryEntity subCategory,
        IAssetCategoryRepository categories,
        CancellationToken cancellationToken)
    {
        var category = MongoObjectId.IsValid(subCategory.CategoryId)
            ? await categories.GetByIdAsync(subCategory.CategoryId, cancellationToken)
            : null;

        if (category is null)
        {
            return Results.BadRequest(
                new { message = $"Asset category '{subCategory.CategoryId}' was not found." });
        }

        subCategory.CategoryName = category.CategoryName;

        return null;
    }
}
