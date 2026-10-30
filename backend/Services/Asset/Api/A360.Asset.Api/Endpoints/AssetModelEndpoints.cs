using A360.Asset.Api.Contracts;
using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

public static class AssetModelEndpoints
{
    private const string EntityType = "AssetModel";

    public static RouteGroupBuilder MapAssetModelEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-models").WithTags("Asset Models");

        group.MapGet("", GetModelsAsync).WithName("GetAssetModels");
        group.MapGet("/{id}", GetModelByIdAsync).WithName("GetAssetModelById");
        group.MapPost("", CreateModelAsync).WithName("CreateAssetModel");
        group.MapPut("/{id}", UpdateModelAsync).WithName("UpdateAssetModel");
        group.MapDelete("/{id}", DeleteModelAsync).WithName("DeleteAssetModel");

        return group;
    }

    private static async Task<IResult> GetModelsAsync(
        IAssetModelRepository repository,
        IAssetCategoryTypeRepository typeRepository,
        CancellationToken cancellationToken,
        string? search = null,
        string? status = null,
        string? assetTypeId = null,
        string? manufacturer = null)
    {
        var models = (await repository.GetAllAsync(cancellationToken)).Where(model => !model.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            models = models.Where(model =>
                model.ModelName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || model.ModelCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            models = models.Where(model => string.Equals(model.Status, status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(assetTypeId))
        {
            models = models.Where(model => model.AssetTypeId == assetTypeId.Trim());
        }

        // Matches either the manufacturer's id or its name.
        if (!string.IsNullOrWhiteSpace(manufacturer))
        {
            var value = manufacturer.Trim();
            models = models.Where(model =>
                model.ManufacturerId == value
                || string.Equals(model.ManufacturerName, value, StringComparison.OrdinalIgnoreCase));
        }

        var typeNames = (await typeRepository.GetAllAsync(cancellationToken)).ToDictionary(type => type.Id, type => type.TypeName);

        return Results.Ok(models
            .OrderBy(model => model.ModelName, StringComparer.OrdinalIgnoreCase)
            .Select(model => AssetModelResponse.FromEntity(model, typeNames.GetValueOrDefault(model.AssetTypeId)))
            .ToList());
    }

    private static async Task<IResult> GetModelByIdAsync(
        string id,
        IAssetModelRepository repository,
        IAssetCategoryTypeRepository typeRepository,
        CancellationToken cancellationToken)
    {
        var model = await repository.GetByIdAsync(id, cancellationToken);
        return model is null || model.IsDeleted
            ? NotFound(id)
            : Results.Ok(await ToResponseAsync(model, typeRepository, cancellationToken));
    }

    private static async Task<IResult> CreateModelAsync(
        SaveAssetModelRequest request,
        IAssetModelRepository repository,
        IAssetCategoryTypeRepository typeRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var model = request.ToEntity(currentUserAccessor.GetCurrentUser());

        var problem = await CheckTypeAndUniquenessAsync(model, null, repository, typeRepository, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        await repository.CreateAsync(model, cancellationToken);
        await eventLogger.LogAsync(EntityType, model.ModelCode, model.ModelName, EventAction.Created, cancellationToken);

        return Results.Created(
            $"/api/asset-models/{model.Id}",
            await ToResponseAsync(model, typeRepository, cancellationToken));
    }

    private static async Task<IResult> UpdateModelAsync(
        string id,
        SaveAssetModelRequest request,
        IAssetModelRepository repository,
        IAssetCategoryTypeRepository typeRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var model = await repository.GetByIdAsync(id, cancellationToken);
        if (model is null || model.IsDeleted)
        {
            return NotFound(id);
        }

        request.ApplyTo(model, currentUserAccessor.GetCurrentUser());

        var problem = await CheckTypeAndUniquenessAsync(model, id, repository, typeRepository, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        if (!await repository.UpdateAsync(id, model, cancellationToken))
        {
            return NotFound(id);
        }

        await eventLogger.LogAsync(EntityType, model.ModelCode, model.ModelName, EventAction.Updated, cancellationToken);

        return Results.Ok(await ToResponseAsync(model, typeRepository, cancellationToken));
    }

    private static async Task<IResult> DeleteModelAsync(
        string id,
        IAssetModelRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var model = await repository.GetByIdAsync(id, cancellationToken);
        if (model is null || model.IsDeleted)
        {
            return NotFound(id);
        }

        if (!await repository.DeleteAsync(id, cancellationToken))
        {
            return NotFound(id);
        }

        await eventLogger.LogAsync(EntityType, model.ModelCode, model.ModelName, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    // excludeId is the model being updated, so it does not clash with itself.
    private static async Task<IResult?> CheckTypeAndUniquenessAsync(
        AssetModel model,
        string? excludeId,
        IAssetModelRepository repository,
        IAssetCategoryTypeRepository typeRepository,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(model.AssetTypeId) || await typeRepository.GetByIdAsync(model.AssetTypeId, cancellationToken) is not { IsDeleted: false })
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["AssetTypeId"] = [$"Asset type '{model.AssetTypeId}' was not found"]
            });
        }

        if (await repository.CodeExistsAsync(model.ModelCode, excludeId, cancellationToken))
        {
            return Results.Conflict(new { message = $"A model with code '{model.ModelCode}' already exists." });
        }

        if (await repository.NameExistsInTypeAsync(model.ModelName, model.AssetTypeId, excludeId, cancellationToken))
        {
            return Results.Conflict(new { message = $"A model named '{model.ModelName}' already exists for this asset type." });
        }

        return null;
    }

    private static async Task<AssetModelResponse> ToResponseAsync(AssetModel model, IAssetCategoryTypeRepository typeRepository, CancellationToken cancellationToken)
    {
        var type = await typeRepository.GetByIdAsync(model.AssetTypeId, cancellationToken);
        return AssetModelResponse.FromEntity(model, type?.TypeName);
    }

    private static IResult NotFound(string id)
    {
        return Results.NotFound(new { message = $"Asset model '{id}' was not found." });
    }
}
