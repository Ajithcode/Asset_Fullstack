using A360.Asset.Api.Contracts;
using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

/// <summary>Asset types within a category. Routed as /api/asset-category-types because /api/asset-types belongs to Master Management.</summary>
public static class AssetCategoryTypeEndpoints
{
    private const string EntityType = "AssetCategoryType";

    public static RouteGroupBuilder MapAssetCategoryTypeEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-category-types").WithTags("Asset Types");

        group.MapGet("", GetTypesAsync).WithName("GetAssetCategoryTypes");
        group.MapGet("/{id}", GetTypeByIdAsync).WithName("GetAssetCategoryTypeById");
        group.MapPost("", CreateTypeAsync).WithName("CreateAssetCategoryType");
        group.MapPut("/{id}", UpdateTypeAsync).WithName("UpdateAssetCategoryType");
        group.MapDelete("/{id}", DeleteTypeAsync).WithName("DeleteAssetCategoryType");

        return group;
    }

    private static async Task<IResult> GetTypesAsync(
        IAssetCategoryTypeRepository repository,
        IAssetCategoryRepository categoryRepository,
        IAssetModelRepository modelRepository,
        CancellationToken cancellationToken,
        string? search = null,
        string? status = null,
        string? categoryId = null)
    {
        var types = (await repository.GetAllAsync(cancellationToken)).Where(type => !type.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            types = types.Where(type =>
                type.TypeName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || type.TypeCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            types = types.Where(type => string.Equals(type.Status, status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            types = types.Where(type => type.CategoryId == categoryId.Trim());
        }

        var categoryNames = (await categoryRepository.GetAllAsync(cancellationToken)).ToDictionary(category => category.Id, category => category.Name);
        var modelCounts = await modelRepository.CountByTypeAsync(cancellationToken);

        return Results.Ok(types
            .OrderBy(type => type.TypeName, StringComparer.OrdinalIgnoreCase)
            .Select(type => AssetCategoryTypeResponse.FromEntity(
                type,
                categoryNames.GetValueOrDefault(type.CategoryId),
                modelCounts.GetValueOrDefault(type.Id)))
            .ToList());
    }

    private static async Task<IResult> GetTypeByIdAsync(
        string id,
        IAssetCategoryTypeRepository repository,
        IAssetCategoryRepository categoryRepository,
        IAssetModelRepository modelRepository,
        CancellationToken cancellationToken)
    {
        var type = await repository.GetByIdAsync(id, cancellationToken);
        return type is null || type.IsDeleted
            ? NotFound(id)
            : Results.Ok(await ToResponseAsync(type, categoryRepository, modelRepository, cancellationToken));
    }

    private static async Task<IResult> CreateTypeAsync(
        CreateAssetCategoryTypeRequest request,
        IAssetCategoryTypeRepository repository,
        IAssetCategoryRepository categoryRepository,
        IAssetModelRepository modelRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var type = request.ToEntity(currentUserAccessor.GetCurrentUser());

        var problem = await CheckCategoryAndUniquenessAsync(type, null, repository, categoryRepository, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        await repository.CreateAsync(type, cancellationToken);
        await eventLogger.LogAsync(EntityType, type.TypeCode, type.TypeName, EventAction.Created, cancellationToken);

        return Results.Created(
            $"/api/asset-category-types/{type.Id}",
            await ToResponseAsync(type, categoryRepository, modelRepository, cancellationToken));
    }

    private static async Task<IResult> UpdateTypeAsync(
        string id,
        UpdateAssetCategoryTypeRequest request,
        IAssetCategoryTypeRepository repository,
        IAssetCategoryRepository categoryRepository,
        IAssetModelRepository modelRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var type = await repository.GetByIdAsync(id, cancellationToken);
        if (type is null || type.IsDeleted)
        {
            return NotFound(id);
        }

        request.ApplyTo(type, currentUserAccessor.GetCurrentUser());

        var problem = await CheckCategoryAndUniquenessAsync(type, id, repository, categoryRepository, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        if (!await repository.UpdateAsync(id, type, cancellationToken))
        {
            return NotFound(id);
        }

        await eventLogger.LogAsync(EntityType, type.TypeCode, type.TypeName, EventAction.Updated, cancellationToken);

        return Results.Ok(await ToResponseAsync(type, categoryRepository, modelRepository, cancellationToken));
    }

    private static async Task<IResult> DeleteTypeAsync(
        string id,
        IAssetCategoryTypeRepository repository,
        IAssetModelRepository modelRepository,
        IAssetFieldLayoutRepository layoutRepository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var type = await repository.GetByIdAsync(id, cancellationToken);
        if (type is null || type.IsDeleted)
        {
            return NotFound(id);
        }

        if (await modelRepository.AnyInTypeAsync(id, cancellationToken))
        {
            return Results.Conflict(new { message = $"Asset type '{type.TypeName}' has models. Delete them first." });
        }

        if ((await layoutRepository.GetAllAsync(cancellationToken)).Any(layout => layout.AssetTypeId == id))
        {
            return Results.Conflict(new { message = $"Asset type '{type.TypeName}' has field layouts. Delete them first." });
        }

        if (!await repository.DeleteAsync(id, cancellationToken))
        {
            return NotFound(id);
        }

        await eventLogger.LogAsync(EntityType, type.TypeCode, type.TypeName, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    // excludeId is the type being updated, so it does not clash with itself.
    private static async Task<IResult?> CheckCategoryAndUniquenessAsync(
        AssetCategoryType type,
        string? excludeId,
        IAssetCategoryTypeRepository repository,
        IAssetCategoryRepository categoryRepository,
        CancellationToken cancellationToken)
    {
        if (!MongoObjectId.IsValid(type.CategoryId) || await categoryRepository.GetByIdAsync(type.CategoryId, cancellationToken) is not { IsDeleted: false })
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["CategoryId"] = [$"Asset category '{type.CategoryId}' was not found"]
            });
        }

        if (await repository.CodeExistsAsync(type.TypeCode, excludeId, cancellationToken))
        {
            return Results.Conflict(new { message = $"An asset type with code '{type.TypeCode}' already exists." });
        }

        if (await repository.NameExistsInCategoryAsync(type.TypeName, type.CategoryId, excludeId, cancellationToken))
        {
            return Results.Conflict(new { message = $"An asset type named '{type.TypeName}' already exists in this category." });
        }

        return null;
    }

    private static async Task<AssetCategoryTypeResponse> ToResponseAsync(
        AssetCategoryType type,
        IAssetCategoryRepository categoryRepository,
        IAssetModelRepository modelRepository,
        CancellationToken cancellationToken)
    {
        var category = await categoryRepository.GetByIdAsync(type.CategoryId, cancellationToken);
        var modelCounts = await modelRepository.CountByTypeAsync(cancellationToken);
        return AssetCategoryTypeResponse.FromEntity(type, category?.Name, modelCounts.GetValueOrDefault(type.Id));
    }

    private static IResult NotFound(string id)
    {
        return Results.NotFound(new { message = $"Asset type '{id}' was not found." });
    }
}
