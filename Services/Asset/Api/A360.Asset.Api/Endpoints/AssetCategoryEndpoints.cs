using A360.Asset.Api.Contracts;
using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

public static class AssetCategoryEndpoints
{
    private const string EntityType = "AssetCategory";
    private const string CustomFieldEntityType = "AssetCategoryCustomField";
    private const string AttributeEntityType = "AssetCategoryAttribute";

    public static RouteGroupBuilder MapAssetCategoryEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-categories").WithTags("Asset Categories");

        group.MapGet("", GetAssetCategoriesAsync).WithName("GetAssetCategories");
        group.MapGet("/{id}", GetAssetCategoryByIdAsync).WithName("GetAssetCategoryById");
        group.MapPost("", CreateAssetCategoryAsync).WithName("CreateAssetCategory");
        group.MapPut("/{id}", UpdateAssetCategoryAsync).WithName("UpdateAssetCategory");
        group.MapDelete("/{id}", DeleteAssetCategoryAsync).WithName("DeleteAssetCategory");

        // Custom fields and attributes are defined on a category and inherited by its sub-categories.
        group.MapPost("/{id}/custom-fields", CreateCustomFieldAsync).WithName("CreateAssetCategoryCustomField");
        group.MapDelete("/{id}/custom-fields/{fieldId}", DeleteCustomFieldAsync).WithName("DeleteAssetCategoryCustomField");
        group.MapPost("/{id}/attributes", CreateAttributeAsync).WithName("CreateAssetCategoryAttribute");
        group.MapDelete("/{id}/attributes/{attributeId}", DeleteAttributeAsync).WithName("DeleteAssetCategoryAttribute");

        return group;
    }

    private static async Task<IResult> GetAssetCategoriesAsync(
        IAssetCategoryRepository repository,
        CancellationToken cancellationToken,
        string? search = null,
        string? status = null,
        string? parentCategoryId = null)
    {
        var categoriesById = await GetCategoriesByIdAsync(repository, cancellationToken);

        var filtered = categoriesById.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(category =>
                category.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || category.Code.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered.Where(category => string.Equals(category.Status, status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // "root" lists top-level categories; an id lists that category's direct children.
        if (!string.IsNullOrWhiteSpace(parentCategoryId))
        {
            var parentId = parentCategoryId.Trim();
            filtered = string.Equals(parentId, "root", StringComparison.OrdinalIgnoreCase)
                ? filtered.Where(category => category.ParentCategoryId is null)
                : filtered.Where(category => category.ParentCategoryId == parentId);
        }

        return Results.Ok(filtered
            .OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
            .Select(category => AssetCategoryResponse.FromEntity(category, categoriesById))
            .ToList());
    }

    private static async Task<IResult> GetAssetCategoryByIdAsync(
        string id,
        IAssetCategoryRepository repository,
        CancellationToken cancellationToken)
    {
        return await CategoryResponseAsync(id, repository, cancellationToken) is { } response
            ? Results.Ok(response)
            : NotFound(id);
    }

    private static async Task<IResult> CreateAssetCategoryAsync(
        CreateAssetCategoryRequest request,
        IAssetCategoryRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var category = request.ToEntity(currentUserAccessor.GetCurrentUser());

        var problem = await CheckParentAndUniquenessAsync(category, null, repository, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        await repository.CreateAsync(category, cancellationToken);
        await eventLogger.LogAsync(EntityType, category.Code, category.Name, EventAction.Created, cancellationToken);

        return Results.Created(
            $"/api/asset-categories/{category.Id}",
            await CategoryResponseAsync(category.Id, repository, cancellationToken));
    }

    private static async Task<IResult> UpdateAssetCategoryAsync(
        string id,
        UpdateAssetCategoryRequest request,
        IAssetCategoryRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var category = await repository.GetByIdAsync(id, cancellationToken);
        if (category is null || category.IsDeleted)
        {
            return NotFound(id);
        }

        request.ApplyTo(category, currentUserAccessor.GetCurrentUser());

        var problem = await CheckParentAndUniquenessAsync(category, id, repository, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        if (!await repository.UpdateDetailsAsync(category, cancellationToken))
        {
            return NotFound(id);
        }

        await eventLogger.LogAsync(EntityType, category.Code, category.Name, EventAction.Updated, cancellationToken);

        return Results.Ok(await CategoryResponseAsync(id, repository, cancellationToken));
    }

    private static async Task<IResult> DeleteAssetCategoryAsync(
        string id,
        IAssetCategoryRepository repository,
        IAssetCategoryTypeRepository typeRepository,
        IAssetCustomFieldRepository customFieldRepository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var category = await repository.GetByIdAsync(id, cancellationToken);
        if (category is null || category.IsDeleted)
        {
            return NotFound(id);
        }

        if (await repository.HasChildrenAsync(id, cancellationToken))
        {
            return Results.Conflict(new { message = $"Category '{category.Name}' has sub-categories. Move or delete them first." });
        }

        if (await typeRepository.AnyInCategoryAsync(id, cancellationToken))
        {
            return Results.Conflict(new { message = $"Category '{category.Name}' has asset types. Move or delete them first." });
        }

        if (await customFieldRepository.AnyAppliesToCategoryAsync(id, cancellationToken))
        {
            return Results.Conflict(new { message = $"Category '{category.Name}' has category specific custom fields. Move or delete them first." });
        }

        if (!await repository.DeleteAsync(id, cancellationToken))
        {
            return NotFound(id);
        }

        await eventLogger.LogAsync(EntityType, category.Code, category.Name, EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateCustomFieldAsync(
        string id,
        CreateAssetCategoryCustomFieldRequest request,
        IAssetCategoryRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var categoriesById = await GetCategoriesByIdAsync(repository, cancellationToken);
        if (!categoriesById.TryGetValue(id, out var category))
        {
            return NotFound(id);
        }

        var fieldName = request.FieldName!.Trim();

        // A name must be unique across everything that would show it: the category, its ancestors and its sub-categories.
        var clash = RelatedCategories(category, categoriesById)
            .FirstOrDefault(related => related.CustomFields.Any(field => string.Equals(field.FieldName, fieldName, StringComparison.OrdinalIgnoreCase)));
        if (clash is not null)
        {
            return Results.Conflict(new { message = $"A custom field named '{fieldName}' is already defined on '{clash.Name}'." });
        }

        var field = new AssetCategoryCustomField
        {
            Id = MongoObjectId.Create(),
            FieldName = fieldName,
            Type = AssetCategoryCustomFieldTypes.Normalize(request.Type)!,
            Required = request.Required ?? false,
            CreatedBy = currentUserAccessor.GetCurrentUser()?.UserName,
            CreatedAt = DateTime.UtcNow
        };

        if (!await repository.AddCustomFieldAsync(id, field, cancellationToken))
        {
            return NotFound(id);
        }

        await eventLogger.LogAsync(CustomFieldEntityType, field.Id, $"{category.Name} - {field.FieldName}", EventAction.Created, cancellationToken);

        return Results.Created(
            $"/api/asset-categories/{id}",
            AssetCategoryCustomFieldResponse.FromEntity(field, category, category));
    }

    private static async Task<IResult> DeleteCustomFieldAsync(
        string id,
        string fieldId,
        IAssetCategoryRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var categoriesById = await GetCategoriesByIdAsync(repository, cancellationToken);
        if (!categoriesById.TryGetValue(id, out var category))
        {
            return NotFound(id);
        }

        var field = category.CustomFields.FirstOrDefault(candidate => candidate.Id == fieldId);
        if (field is null)
        {
            // An inherited field can only be removed from the category it was defined on.
            var owner = AssetCategoryLineage.SelfAndAncestors(category, categoriesById)
                .FirstOrDefault(ancestor => ancestor.CustomFields.Any(candidate => candidate.Id == fieldId));

            return owner is null
                ? Results.NotFound(new { message = $"Custom field '{fieldId}' was not found on category '{category.Name}'." })
                : Results.Conflict(new { message = $"This custom field is inherited from '{owner.Name}'. Delete it there." });
        }

        if (!await repository.RemoveCustomFieldAsync(id, fieldId, cancellationToken))
        {
            return Results.NotFound(new { message = $"Custom field '{fieldId}' was not found on category '{category.Name}'." });
        }

        await eventLogger.LogAsync(CustomFieldEntityType, field.Id, $"{category.Name} - {field.FieldName}", EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateAttributeAsync(
        string id,
        CreateAssetCategoryAttributeRequest request,
        IAssetCategoryRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var categoriesById = await GetCategoriesByIdAsync(repository, cancellationToken);
        if (!categoriesById.TryGetValue(id, out var category))
        {
            return NotFound(id);
        }

        var attributeName = request.Attribute!.Trim();

        var clash = RelatedCategories(category, categoriesById)
            .FirstOrDefault(related => related.Attributes.Any(attribute => string.Equals(attribute.Attribute, attributeName, StringComparison.OrdinalIgnoreCase)));
        if (clash is not null)
        {
            return Results.Conflict(new { message = $"An attribute named '{attributeName}' is already defined on '{clash.Name}'." });
        }

        var attribute = new AssetCategoryAttribute
        {
            Id = MongoObjectId.Create(),
            Attribute = attributeName,
            Unit = ContractText.TrimOrNull(request.Unit),
            ExampleValue = ContractText.TrimOrNull(request.ExampleValue),
            CreatedBy = currentUserAccessor.GetCurrentUser()?.UserName,
            CreatedAt = DateTime.UtcNow
        };

        if (!await repository.AddAttributeAsync(id, attribute, cancellationToken))
        {
            return NotFound(id);
        }

        await eventLogger.LogAsync(AttributeEntityType, attribute.Id, $"{category.Name} - {attribute.Attribute}", EventAction.Created, cancellationToken);

        return Results.Created(
            $"/api/asset-categories/{id}",
            AssetCategoryAttributeResponse.FromEntity(attribute, category, category));
    }

    private static async Task<IResult> DeleteAttributeAsync(
        string id,
        string attributeId,
        IAssetCategoryRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var categoriesById = await GetCategoriesByIdAsync(repository, cancellationToken);
        if (!categoriesById.TryGetValue(id, out var category))
        {
            return NotFound(id);
        }

        var attribute = category.Attributes.FirstOrDefault(candidate => candidate.Id == attributeId);
        if (attribute is null)
        {
            // An inherited attribute can only be removed from the category it was defined on.
            var owner = AssetCategoryLineage.SelfAndAncestors(category, categoriesById)
                .FirstOrDefault(ancestor => ancestor.Attributes.Any(candidate => candidate.Id == attributeId));

            return owner is null
                ? Results.NotFound(new { message = $"Attribute '{attributeId}' was not found on category '{category.Name}'." })
                : Results.Conflict(new { message = $"This attribute is inherited from '{owner.Name}'. Delete it there." });
        }

        if (!await repository.RemoveAttributeAsync(id, attributeId, cancellationToken))
        {
            return Results.NotFound(new { message = $"Attribute '{attributeId}' was not found on category '{category.Name}'." });
        }

        await eventLogger.LogAsync(AttributeEntityType, attribute.Id, $"{category.Name} - {attribute.Attribute}", EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    // excludeId is the category being updated, so it does not clash with itself.
    private static async Task<IResult?> CheckParentAndUniquenessAsync(
        AssetCategory category,
        string? excludeId,
        IAssetCategoryRepository repository,
        CancellationToken cancellationToken)
    {
        if (category.ParentCategoryId is { } parentId)
        {
            if (!MongoObjectId.IsValid(parentId) || await repository.GetByIdAsync(parentId, cancellationToken) is not { IsDeleted: false })
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["ParentCategoryId"] = [$"Parent category '{parentId}' was not found"]
                });
            }

            if (excludeId is not null && await IsSelfOrDescendantAsync(parentId, excludeId, repository, cancellationToken))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["ParentCategoryId"] = ["A category cannot be placed under itself or one of its own sub-categories"]
                });
            }
        }

        if (await repository.CodeExistsAsync(category.Code, excludeId, cancellationToken))
        {
            return Results.Conflict(new { message = $"A category with code '{category.Code}' already exists." });
        }

        if (await repository.NameExistsUnderParentAsync(category.Name, category.ParentCategoryId, excludeId, cancellationToken))
        {
            return Results.Conflict(new { message = $"A category named '{category.Name}' already exists under the same parent." });
        }

        return null;
    }

    // Walks up from the proposed parent; reaching the category being moved means the move would create a loop.
    private static async Task<bool> IsSelfOrDescendantAsync(
        string parentId,
        string categoryId,
        IAssetCategoryRepository repository,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<string>();
        string? currentId = parentId;

        while (currentId is not null && visited.Add(currentId))
        {
            if (currentId == categoryId)
            {
                return true;
            }

            currentId = (await repository.GetByIdAsync(currentId, cancellationToken))?.ParentCategoryId;
        }

        return false;
    }

    // The category, its ancestors (whose items it inherits) and its sub-categories (which inherit its items).
    private static IEnumerable<AssetCategory> RelatedCategories(AssetCategory category, IReadOnlyDictionary<string, AssetCategory> categoriesById)
    {
        return AssetCategoryLineage.SelfAndAncestors(category, categoriesById)
            .Concat(AssetCategoryLineage.Descendants(category, categoriesById.Values.ToList()));
    }

    // Categories are a small master list, so they are loaded together to resolve parents and inheritance.
    private static async Task<Dictionary<string, AssetCategory>> GetCategoriesByIdAsync(IAssetCategoryRepository repository, CancellationToken cancellationToken)
    {
        var categories = await repository.GetAllAsync(cancellationToken);
        return categories
            .Where(category => !category.IsDeleted)
            .ToDictionary(category => category.Id);
    }

    private static async Task<AssetCategoryResponse?> CategoryResponseAsync(string id, IAssetCategoryRepository repository, CancellationToken cancellationToken)
    {
        var categoriesById = await GetCategoriesByIdAsync(repository, cancellationToken);
        return categoriesById.TryGetValue(id, out var category)
            ? AssetCategoryResponse.FromEntity(category, categoriesById)
            : null;
    }

    private static IResult NotFound(string id)
    {
        return Results.NotFound(new { message = $"Asset category '{id}' was not found." });
    }
}
