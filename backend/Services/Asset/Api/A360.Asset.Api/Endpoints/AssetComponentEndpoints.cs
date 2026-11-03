using A360.Asset.Api.Contracts;
using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

/// <summary>Components of a registered asset, addressed by the asset's registration id (e.g. AR000001).</summary>
public static class AssetComponentEndpoints
{
    private const string EntityType = "AssetComponent";
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;

    public static RouteGroupBuilder MapAssetComponentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-registrations/{assetRegistrationId}/components").WithTags("Asset Components");

        group.MapGet("", GetComponentsAsync).WithName("GetAssetComponents");
        group.MapGet("/tree", GetComponentTreeAsync).WithName("GetAssetComponentTree");
        group.MapGet("/{componentId}", GetComponentByIdAsync).WithName("GetAssetComponentById");
        group.MapPost("", CreateComponentAsync).WithName("CreateAssetComponent");
        group.MapPut("/{componentId}", UpdateComponentAsync).WithName("UpdateAssetComponent");
        group.MapDelete("/{componentId}", DeleteComponentAsync).WithName("DeleteAssetComponent");

        return group;
    }

    private static async Task<IResult> GetComponentsAsync(
        string assetRegistrationId,
        IAssetComponentRepository repository,
        IAssetRegistrationRepository assetRepository,
        CancellationToken cancellationToken,
        string? search = null,
        string? status = null,
        string? parentComponentId = null,
        int page = 1,
        int pageSize = DefaultPageSize)
    {
        if (await assetRepository.GetByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken) is null)
        {
            return AssetNotFound(assetRegistrationId);
        }

        var all = await repository.GetByAssetAsync(assetRegistrationId, cancellationToken);
        var components = all.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            components = components.Where(component =>
                component.ComponentName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || component.ComponentTag.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (component.PartNumber?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || (component.SerialNumber?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            components = components.Where(component => string.Equals(component.Status, status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // "root" lists top-level components; an id lists that component's direct children.
        if (!string.IsNullOrWhiteSpace(parentComponentId))
        {
            var parentId = parentComponentId.Trim();
            components = string.Equals(parentId, "root", StringComparison.OrdinalIgnoreCase)
                ? components.Where(component => component.ParentComponentId is null)
                : components.Where(component => component.ParentComponentId == parentId);
        }

        var filtered = components.OrderBy(component => component.ComponentName, StringComparer.OrdinalIgnoreCase).ToList();

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > MaxPageSize ? DefaultPageSize : pageSize;

        var lookup = new ComponentLookup(all);
        return Results.Ok(new AssetComponentListResponse(
            filtered.Skip((page - 1) * pageSize).Take(pageSize).Select(lookup.ToResponse).ToList(),
            filtered.Count,
            page,
            pageSize));
    }

    private static async Task<IResult> GetComponentTreeAsync(
        string assetRegistrationId,
        IAssetComponentRepository repository,
        IAssetRegistrationRepository assetRepository,
        CancellationToken cancellationToken)
    {
        if (await assetRepository.GetByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken) is null)
        {
            return AssetNotFound(assetRegistrationId);
        }

        var all = await repository.GetByAssetAsync(assetRegistrationId, cancellationToken);
        var childrenByParent = all.ToLookup(component => component.ParentComponentId ?? string.Empty);

        IReadOnlyList<AssetComponentNodeResponse> Build(string parentKey, HashSet<string> path) =>
            childrenByParent[parentKey]
                .Where(child => !path.Contains(child.Id))
                .OrderBy(child => child.ComponentName, StringComparer.OrdinalIgnoreCase)
                .Select(child => new AssetComponentNodeResponse(
                    child.Id,
                    child.ComponentName,
                    child.ComponentTag,
                    child.Status,
                    AssetComponentResponse.LifecyclePercentOf(child),
                    child.ExpectedReplacementDate,
                    Build(child.Id, [.. path, child.Id])))
                .ToList();

        return Results.Ok(Build(string.Empty, []));
    }

    private static async Task<IResult> GetComponentByIdAsync(
        string assetRegistrationId,
        string componentId,
        IAssetComponentRepository repository,
        CancellationToken cancellationToken)
    {
        var all = await repository.GetByAssetAsync(assetRegistrationId, cancellationToken);
        var component = all.FirstOrDefault(candidate => candidate.Id == componentId);

        return component is null
            ? ComponentNotFound(componentId)
            : Results.Ok(new ComponentLookup(all).ToResponse(component));
    }

    private static async Task<IResult> CreateComponentAsync(
        string assetRegistrationId,
        SaveAssetComponentRequest request,
        IAssetComponentRepository repository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        if (await assetRepository.GetByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken) is null)
        {
            return AssetNotFound(assetRegistrationId);
        }

        var component = request.ToEntity(assetRegistrationId, currentUserAccessor.GetCurrentUser());

        var problem = await CheckParentAndTagAsync(component, null, repository, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        await repository.CreateAsync(component, cancellationToken);
        await eventLogger.LogAsync(EntityType, component.ComponentTag, $"{assetRegistrationId} - {component.ComponentName}", EventAction.Created, cancellationToken);

        return Results.Created(
            $"/api/asset-registrations/{assetRegistrationId}/components/{component.Id}",
            new ComponentLookup(await repository.GetByAssetAsync(assetRegistrationId, cancellationToken)).ToResponse(component));
    }

    private static async Task<IResult> UpdateComponentAsync(
        string assetRegistrationId,
        string componentId,
        SaveAssetComponentRequest request,
        IAssetComponentRepository repository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var component = await repository.GetByIdAsync(componentId, cancellationToken);
        if (component is null || component.IsDeleted || component.AssetRegistrationId != assetRegistrationId)
        {
            return ComponentNotFound(componentId);
        }

        request.ApplyTo(component, currentUserAccessor.GetCurrentUser());

        var problem = await CheckParentAndTagAsync(component, componentId, repository, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        if (!await repository.UpdateAsync(componentId, component, cancellationToken))
        {
            return ComponentNotFound(componentId);
        }

        await eventLogger.LogAsync(EntityType, component.ComponentTag, $"{assetRegistrationId} - {component.ComponentName}", EventAction.Updated, cancellationToken);

        return Results.Ok(new ComponentLookup(await repository.GetByAssetAsync(assetRegistrationId, cancellationToken)).ToResponse(component));
    }

    private static async Task<IResult> DeleteComponentAsync(
        string assetRegistrationId,
        string componentId,
        IAssetComponentRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var component = await repository.GetByIdAsync(componentId, cancellationToken);
        if (component is null || component.IsDeleted || component.AssetRegistrationId != assetRegistrationId)
        {
            return ComponentNotFound(componentId);
        }

        if (await repository.HasChildrenAsync(componentId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Component '{component.ComponentName}' has sub-components. Move or delete them first." });
        }

        if (!await repository.DeleteAsync(componentId, cancellationToken))
        {
            return ComponentNotFound(componentId);
        }

        await eventLogger.LogAsync(EntityType, component.ComponentTag, $"{assetRegistrationId} - {component.ComponentName}", EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    /// <param name="excludeId">The component being updated, so it does not clash with itself.</param>
    private static async Task<IResult?> CheckParentAndTagAsync(
        AssetComponent component,
        string? excludeId,
        IAssetComponentRepository repository,
        CancellationToken cancellationToken)
    {
        if (component.ParentComponentId is { } parentId)
        {
            var parent = MongoObjectId.IsValid(parentId) ? await repository.GetByIdAsync(parentId, cancellationToken) : null;

            // The parent must be a component of the same asset.
            if (parent is not { IsDeleted: false } || parent.AssetRegistrationId != component.AssetRegistrationId)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["ParentComponentId"] = [$"Parent component '{parentId}' was not found on this asset"]
                });
            }

            if (excludeId is not null && await IsSelfOrDescendantAsync(parentId, excludeId, repository, cancellationToken))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["ParentComponentId"] = ["A component cannot be placed under itself or one of its own sub-components"]
                });
            }
        }

        if (await repository.TagExistsAsync(component.AssetRegistrationId, component.ComponentTag, excludeId, cancellationToken))
        {
            return Results.Conflict(new { message = $"Component tag '{component.ComponentTag}' is already used on this asset." });
        }

        return null;
    }

    // Walks up from the proposed parent; reaching the component being moved means the move would create a loop.
    private static async Task<bool> IsSelfOrDescendantAsync(
        string parentId,
        string componentId,
        IAssetComponentRepository repository,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<string>();
        string? currentId = parentId;

        while (currentId is not null && visited.Add(currentId))
        {
            if (currentId == componentId)
            {
                return true;
            }

            currentId = (await repository.GetByIdAsync(currentId, cancellationToken))?.ParentComponentId;
        }

        return false;
    }

    private static IResult AssetNotFound(string assetRegistrationId)
    {
        return Results.NotFound(new { message = $"Asset registration '{assetRegistrationId}' was not found." });
    }

    private static IResult ComponentNotFound(string componentId)
    {
        return Results.NotFound(new { message = $"Component '{componentId}' was not found on this asset." });
    }

    /// <summary>Resolves parent names and child counts from one load of the asset's components.</summary>
    private sealed class ComponentLookup(IReadOnlyCollection<AssetComponent> components)
    {
        private readonly Dictionary<string, string> _namesById = components.ToDictionary(component => component.Id, component => component.ComponentName);
        private readonly Dictionary<string, int> _childCounts = components
            .Where(component => component.ParentComponentId is not null)
            .GroupBy(component => component.ParentComponentId!)
            .ToDictionary(group => group.Key, group => group.Count());

        public AssetComponentResponse ToResponse(AssetComponent component)
        {
            return AssetComponentResponse.FromEntity(
                component,
                component.ParentComponentId is not null ? _namesById.GetValueOrDefault(component.ParentComponentId) : null,
                _childCounts.GetValueOrDefault(component.Id));
        }
    }
}
