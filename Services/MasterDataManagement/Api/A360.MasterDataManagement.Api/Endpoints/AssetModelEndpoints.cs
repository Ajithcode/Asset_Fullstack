using AssetModelEntity = A360.MasterDataManagement.Domain.Entities.AssetModel;
using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Api.Validation;
using A360.MasterDataManagement.Domain.ValueObjects;
using A360.MasterDataManagement.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using MongoDB.Driver;

namespace A360.MasterDataManagement.Api.Endpoints;

public static class AssetModelEndpoints
{
    public static RouteGroupBuilder MapAssetModelEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/master-data/asset-models")
            .WithTags("AssetModels");

        group.MapGet("", GetAssetModelsAsync)
            .WithName("GetAssetModels");

        group.MapGet("/{id}", GetAssetModelByIdAsync)
            .WithName("GetAssetModelById");

        group.MapPost("", CreateAssetModelAsync)
            .WithName("CreateAssetModel");

        group.MapPut("/{id}", UpdateAssetModelAsync)
            .WithName("UpdateAssetModel");

        group.MapPatch("/{id}/status", UpdateAssetModelStatusAsync)
            .WithName("UpdateAssetModelStatus");

        group.MapDelete("/{id}", DeleteAssetModelAsync)
            .WithName("DeleteAssetModel");

        return group;
    }

    private static async Task<IResult> GetAssetModelsAsync(
        string? search,
        string? status,
        string? manufacturerId,
        string? assetTypeId,
        string? categoryId,
        string? lifecycle,
        string? clientId,
        IAssetModelRepository repository,
        CancellationToken cancellationToken)
    {
        var models = await repository.SearchAsync(
            new AssetModelFilter(search, status, manufacturerId, assetTypeId, categoryId, lifecycle, clientId),
            cancellationToken);

        return Results.Ok(
            models.Select(AssetModelResponse.FromEntity));
    }

    private static async Task<IResult> GetAssetModelByIdAsync(
        string id,
        IAssetModelRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid asset model id." });
        }

        var model = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (model is null)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "AssetModel",
            model.Id,
            model.ModelName,
            EventAction.Viewed,
            cancellationToken);

        return Results.Ok(AssetModelResponse.FromEntity(model));
    }

    private static async Task<IResult> CreateAssetModelAsync(
        CreateAssetModelRequest request,
        IAssetModelRepository repository,
        IManufacturerRepository manufacturers,
        IMongoDatabase database,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var model = request.ToEntity();

        var existing = await repository.GetByModelCodeAsync(
            model.ModelCode,
            model.ClientId,
            cancellationToken);

        if (existing is not null)
        {
            return Results.Conflict(
                new { message = $"An asset model with code '{model.ModelCode}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            model,
            manufacturers,
            database,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        model = await repository.CreateAsync(
            model,
            cancellationToken);

        await eventLogger.LogAsync(
            "AssetModel",
            model.Id,
            model.ModelName,
            EventAction.Created,
            cancellationToken);

        return Results.Created(
            $"/api/master-data/asset-models/{model.Id}",
            AssetModelResponse.FromEntity(model));
    }

    private static async Task<IResult> UpdateAssetModelAsync(
        string id,
        UpdateAssetModelRequest request,
        IAssetModelRepository repository,
        IManufacturerRepository manufacturers,
        IMongoDatabase database,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid asset model id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var model = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (model is null)
        {
            return Results.NotFound();
        }

        request.ApplyTo(model);

        var existing = await repository.GetByModelCodeAsync(
            model.ModelCode,
            model.ClientId,
            cancellationToken);

        if (existing is not null && existing.Id != id)
        {
            return Results.Conflict(
                new { message = $"An asset model with code '{model.ModelCode}' already exists." });
        }

        var referenceError = await ResolveReferencesAsync(
            model,
            manufacturers,
            database,
            cancellationToken);

        if (referenceError is not null)
        {
            return referenceError;
        }

        var updated = await repository.UpdateAsync(
            id,
            model,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "AssetModel",
            model.Id,
            model.ModelName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(AssetModelResponse.FromEntity(model));
    }

    private static async Task<IResult> UpdateAssetModelStatusAsync(
        string id,
        UpdateAssetModelStatusRequest request,
        IAssetModelRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid asset model id." });
        }

        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var model = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (model is null)
        {
            return Results.NotFound();
        }

        model.Status = request.Status;
        model.UpdatedBy = request.UpdatedBy;
        model.UpdatedAt = DateTime.UtcNow;

        var updated = await repository.UpdateAsync(
            id,
            model,
            cancellationToken);

        if (!updated)
        {
            return Results.NotFound();
        }

        await eventLogger.LogAsync(
            "AssetModel",
            model.Id,
            model.ModelName,
            EventAction.Updated,
            cancellationToken);

        return Results.Ok(AssetModelResponse.FromEntity(model));
    }

    private static async Task<IResult> DeleteAssetModelAsync(
        string id,
        IAssetModelRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(id))
        {
            return Results.BadRequest(
                new { message = "Invalid asset model id." });
        }

        var model = await repository.GetByIdAsync(
            id,
            cancellationToken);

        if (model is null)
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
            "AssetModel",
            model.Id,
            model.ModelName,
            EventAction.Deleted,
            cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Checks the manufacturer exists, and that the category and asset type exist in the Asset service's masters (the
    /// same category / sub-category / type lists Asset Registration uses), copying their current names onto the model.
    /// The category is a top-level category; the type must belong to it or to one of its sub-categories. When no category
    /// is given the type's (top-level) category is used. Returns an error result, or null when valid.
    /// </summary>
    private static async Task<IResult?> ResolveReferencesAsync(
        AssetModelEntity model,
        IManufacturerRepository manufacturers,
        IMongoDatabase database,
        CancellationToken cancellationToken)
    {
        if (model.Manufacturer is not null)
        {
            var manufacturer = MongoObjectId.IsValid(model.Manufacturer.Id!)
                ? await manufacturers.GetByIdAsync(model.Manufacturer.Id!, cancellationToken)
                : null;

            if (manufacturer is null)
            {
                return Results.BadRequest(
                    new { message = $"Manufacturer '{model.Manufacturer.Id}' was not found." });
            }

            model.Manufacturer.Name = manufacturer.ManufacturerName;
        }

        AssetCatalogLookup.CatalogCategory? typeCategory = null;
        AssetCatalogLookup.CatalogCategory? typeSubCategory = null;

        if (model.AssetType is not null)
        {
            var assetType = await AssetCatalogLookup.FindTypeAsync(database, model.AssetType.Id, cancellationToken);

            if (assetType is null)
            {
                return Results.BadRequest(
                    new { message = $"Asset type '{model.AssetType.Id}' was not found." });
            }

            model.AssetType.Name = assetType.Name;
            typeCategory = await AssetCatalogLookup.FindCategoryAsync(database, assetType.CategoryId, cancellationToken);

            // A type under a sub-category belongs to that sub-category's parent category
            if (typeCategory?.ParentId is not null)
            {
                typeSubCategory = typeCategory;
                typeCategory = await AssetCatalogLookup.FindCategoryAsync(database, typeCategory.ParentId, cancellationToken) ?? typeCategory;
            }
        }

        if (model.Category is null && typeCategory is not null)
        {
            model.Category = new MasterDataReference { Id = typeCategory.Id, Name = typeCategory.Name };
        }

        if (model.SubCategory is null && typeSubCategory is not null)
        {
            model.SubCategory = new MasterDataReference { Id = typeSubCategory.Id, Name = typeSubCategory.Name };
        }

        if (model.Category is null)
        {
            return model.SubCategory is null
                ? null
                : Results.BadRequest(new { message = "Select a category for the sub category." });
        }

        var category = await AssetCatalogLookup.FindCategoryAsync(database, model.Category.Id, cancellationToken);

        if (category is null)
        {
            return Results.BadRequest(
                new { message = $"Asset category '{model.Category.Id}' was not found." });
        }

        model.Category.Name = category.Name;

        if (typeCategory is not null && typeCategory.Id != category.Id)
        {
            return Results.BadRequest(
                new { message = $"Asset type '{model.AssetType!.Name}' belongs to category '{typeCategory.Name}', not '{category.Name}'." });
        }

        if (model.SubCategory is not null)
        {
            var subCategory = await AssetCatalogLookup.FindCategoryAsync(database, model.SubCategory.Id, cancellationToken);

            if (subCategory is null || subCategory.ParentId != category.Id)
            {
                return Results.BadRequest(
                    new { message = $"Sub category '{model.SubCategory.Id}' was not found under category '{category.Name}'." });
            }

            model.SubCategory.Name = subCategory.Name;

            if (typeSubCategory is not null && typeSubCategory.Id != subCategory.Id)
            {
                return Results.BadRequest(
                    new { message = $"Asset type '{model.AssetType!.Name}' belongs to sub category '{typeSubCategory.Name}', not '{subCategory.Name}'." });
            }
        }

        return null;
    }
}
