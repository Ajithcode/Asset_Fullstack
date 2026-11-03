using A360.Asset.Api.Contracts;
using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

public static class AssetHierarchyEndpoints
{
    private const string EntityType = "AssetHierarchyItem";

    public static RouteGroupBuilder MapAssetHierarchyEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-hierarchy").WithTags("Asset Hierarchy");

        group.MapGet("", GetItemsAsync).WithName("GetAssetHierarchyItems");
        group.MapGet("/tree", GetTreeAsync).WithName("GetAssetHierarchyTree");
        group.MapGet("/{id}", GetItemByIdAsync).WithName("GetAssetHierarchyItemById");
        group.MapPost("", CreateItemAsync).WithName("CreateAssetHierarchyItem");
        group.MapPut("/{id}", UpdateItemAsync).WithName("UpdateAssetHierarchyItem");

        return group;
    }

    private static async Task<IResult> GetItemsAsync(
        IAssetHierarchyItemRepository repository,
        CancellationToken cancellationToken,
        string? search = null,
        string? itemKind = null,
        string? status = null,
        string? parentId = null,
        string? assetTypeId = null)
    {
        var all = await LoadItemsAsync(repository, cancellationToken);
        var items = all.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            items = items.Where(item =>
                item.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (item.AssetTag?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(itemKind))
        {
            items = items.Where(item => string.Equals(item.ItemKind, itemKind.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            items = items.Where(item => string.Equals(item.Status, status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // "root" lists top-level items; an id lists that item's direct children.
        if (!string.IsNullOrWhiteSpace(parentId))
        {
            var parent = parentId.Trim();
            items = string.Equals(parent, "root", StringComparison.OrdinalIgnoreCase)
                ? items.Where(item => item.ParentId is null)
                : items.Where(item => item.ParentId == parent);
        }

        if (!string.IsNullOrWhiteSpace(assetTypeId))
        {
            items = items.Where(item => item.AssetTypeId == assetTypeId.Trim());
        }

        var lookup = new HierarchyLookup(all);
        return Results.Ok(items
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(lookup.ToResponse)
            .ToList());
    }

    private static async Task<IResult> GetTreeAsync(
        IAssetHierarchyItemRepository repository,
        CancellationToken cancellationToken,
        string? rootId = null)
    {
        var all = await LoadItemsAsync(repository, cancellationToken);
        var childrenByParent = all.ToLookup(item => item.ParentId ?? string.Empty);

        IReadOnlyList<AssetHierarchyNodeResponse> Build(string parentKey, HashSet<string> path) =>
            childrenByParent[parentKey]
                .Where(child => !path.Contains(child.Id))
                .OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
                .Select(child => new AssetHierarchyNodeResponse(
                    child.Id,
                    child.ItemKind,
                    child.Name,
                    child.AssetTag,
                    child.AssetTypeName,
                    child.ModelName,
                    child.ManufacturerName,
                    child.Status,
                    Build(child.Id, [.. path, child.Id])))
                .ToList();

        // With rootId, returns that item's branch; otherwise the whole tree from the top-level items.
        if (string.IsNullOrWhiteSpace(rootId))
        {
            return Results.Ok(Build(string.Empty, []));
        }

        var root = all.FirstOrDefault(item => item.Id == rootId.Trim());
        if (root is null)
        {
            return NotFound(rootId);
        }

        return Results.Ok(new[]
        {
            new AssetHierarchyNodeResponse(
                root.Id, root.ItemKind, root.Name, root.AssetTag, root.AssetTypeName, root.ModelName, root.ManufacturerName, root.Status,
                Build(root.Id, [root.Id]))
        });
    }

    private static async Task<IResult> GetItemByIdAsync(
        string id,
        IAssetHierarchyItemRepository repository,
        CancellationToken cancellationToken)
    {
        var all = await LoadItemsAsync(repository, cancellationToken);
        var item = all.FirstOrDefault(candidate => candidate.Id == id);

        return item is null
            ? NotFound(id)
            : Results.Ok(new HierarchyLookup(all).ToResponse(item));
    }

    private static async Task<IResult> CreateItemAsync(
        SaveAssetHierarchyItemRequest request,
        IAssetHierarchyItemRepository repository,
        IAssetCategoryTypeRepository typeRepository,
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

        var item = request.ToEntity(currentUserAccessor.GetCurrentUser());

        var problem = await ResolveAndCheckAsync(item, null, repository, typeRepository, modelRepository, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        await repository.CreateAsync(item, cancellationToken);
        await eventLogger.LogAsync(EntityType, item.Id, item.Name, EventAction.Created, cancellationToken);

        return Results.Created(
            $"/api/asset-hierarchy/{item.Id}",
            new HierarchyLookup(await LoadItemsAsync(repository, cancellationToken)).ToResponse(item));
    }

    private static async Task<IResult> UpdateItemAsync(
        string id,
        SaveAssetHierarchyItemRequest request,
        IAssetHierarchyItemRepository repository,
        IAssetCategoryTypeRepository typeRepository,
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

        var item = await repository.GetByIdAsync(id, cancellationToken);
        if (item is null || item.IsDeleted)
        {
            return NotFound(id);
        }

        request.ApplyTo(item, currentUserAccessor.GetCurrentUser());

        var problem = await ResolveAndCheckAsync(item, id, repository, typeRepository, modelRepository, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        if (!await repository.UpdateAsync(id, item, cancellationToken))
        {
            return NotFound(id);
        }

        await eventLogger.LogAsync(EntityType, item.Id, item.Name, EventAction.Updated, cancellationToken);

        return Results.Ok(new HierarchyLookup(await LoadItemsAsync(repository, cancellationToken)).ToResponse(item));
    }

    /// <summary>
    /// Checks the parent, asset type and model links and fills in names from the masters.
    /// A model on its own also sets its asset type, and its manufacturer when none was chosen.
    /// </summary>
    /// <param name="excludeId">The item being edited, so it does not clash with itself.</param>
    private static async Task<IResult?> ResolveAndCheckAsync(
        AssetHierarchyItem item,
        string? excludeId,
        IAssetHierarchyItemRepository repository,
        IAssetCategoryTypeRepository typeRepository,
        IAssetModelRepository modelRepository,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (item.ParentId is { } parentId)
        {
            if (!MongoObjectId.IsValid(parentId) || await repository.GetByIdAsync(parentId, cancellationToken) is not { IsDeleted: false })
            {
                errors["ParentId"] = [$"Parent item '{parentId}' was not found"];
            }
            else if (excludeId is not null && await IsSelfOrDescendantAsync(parentId, excludeId, repository, cancellationToken))
            {
                errors["ParentId"] = ["An item cannot be placed under itself or one of its own children"];
            }
        }

        AssetModel? model = null;
        if (item.ModelId is { } modelId)
        {
            model = MongoObjectId.IsValid(modelId) ? await modelRepository.GetByIdAsync(modelId, cancellationToken) : null;
            if (model is not { IsDeleted: false })
            {
                errors["ModelId"] = [$"Model '{modelId}' was not found"];
                model = null;
            }
            else if (item.AssetTypeId is not null && item.AssetTypeId != model.AssetTypeId)
            {
                errors["ModelId"] = [$"Model '{model.ModelName}' does not belong to the selected asset type"];
            }
        }

        item.AssetTypeId ??= model?.AssetTypeId;

        if (item.AssetTypeId is { } assetTypeId)
        {
            var type = MongoObjectId.IsValid(assetTypeId) ? await typeRepository.GetByIdAsync(assetTypeId, cancellationToken) : null;
            if (type is not { IsDeleted: false })
            {
                errors["AssetTypeId"] = [$"Asset type '{assetTypeId}' was not found"];
            }
            else
            {
                item.AssetTypeName = type.TypeName;
            }
        }
        else
        {
            item.AssetTypeName = null;
        }

        item.ModelName = model?.ModelName;
        if (model is not null && item.ManufacturerName is null && item.ManufacturerId is null)
        {
            item.ManufacturerId = model.ManufacturerId;
            item.ManufacturerName = model.ManufacturerName;
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (await repository.NameExistsUnderParentAsync(item.Name, item.ParentId, excludeId, cancellationToken))
        {
            return Results.Conflict(new { message = $"An item named '{item.Name}' already exists under the same parent." });
        }

        if (item.AssetTag is not null && await repository.AssetTagExistsAsync(item.AssetTag, excludeId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Asset tag '{item.AssetTag}' is already used by another item in the hierarchy." });
        }

        return null;
    }

    // Walks up from the proposed parent; reaching the item being moved means the move would create a loop.
    private static async Task<bool> IsSelfOrDescendantAsync(
        string parentId,
        string itemId,
        IAssetHierarchyItemRepository repository,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<string>();
        string? currentId = parentId;

        while (currentId is not null && visited.Add(currentId))
        {
            if (currentId == itemId)
            {
                return true;
            }

            currentId = (await repository.GetByIdAsync(currentId, cancellationToken))?.ParentId;
        }

        return false;
    }

    private static async Task<List<AssetHierarchyItem>> LoadItemsAsync(IAssetHierarchyItemRepository repository, CancellationToken cancellationToken)
    {
        return (await repository.GetAllAsync(cancellationToken)).Where(item => !item.IsDeleted).ToList();
    }

    private static IResult NotFound(string id)
    {
        return Results.NotFound(new { message = $"Asset hierarchy item '{id}' was not found." });
    }

    /// <summary>Resolves parent names and child counts from one load of all items.</summary>
    private sealed class HierarchyLookup(IReadOnlyCollection<AssetHierarchyItem> items)
    {
        private readonly Dictionary<string, string> _namesById = items.ToDictionary(item => item.Id, item => item.Name);
        private readonly Dictionary<string, int> _childCounts = items
            .Where(item => item.ParentId is not null)
            .GroupBy(item => item.ParentId!)
            .ToDictionary(group => group.Key, group => group.Count());

        public AssetHierarchyItemResponse ToResponse(AssetHierarchyItem item)
        {
            return AssetHierarchyItemResponse.FromEntity(
                item,
                item.ParentId is not null ? _namesById.GetValueOrDefault(item.ParentId) : null,
                _childCounts.GetValueOrDefault(item.Id));
        }
    }
}
