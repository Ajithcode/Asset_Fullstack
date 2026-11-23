using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

/// <summary>Body of POST / PUT component-structure: a node name, its parent node, an optional linked component and a description.</summary>
public sealed record SaveComponentStructureNodeRequest(string? Name, string? ParentNodeId, string? LinkedComponentId, string? Description);

public sealed record ComponentStructureNodeResponse(
    string Id,
    string AssetRegistrationId,
    string? ParentNodeId,
    string Name,
    string? LinkedComponentId,
    string? LinkedComponentName,
    string? LinkedComponentTag,
    string? Description,
    DateTime? CreatedAt);

/// <summary>The Component Structure of a registered asset: named nodes that nest and can point at one of the asset's components.</summary>
public static class AssetComponentStructureEndpoints
{
    private const string EntityType = "AssetComponentStructure";
    private const int MaxNameLength = 120;
    private const int MaxDescriptionLength = 500;

    public static RouteGroupBuilder MapAssetComponentStructureEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-registrations/{assetRegistrationId}/component-structure").WithTags("Asset Component Structure");

        group.MapGet("", GetNodesAsync).WithName("GetAssetComponentStructure");
        group.MapPost("", CreateNodeAsync).WithName("CreateAssetComponentStructureNode");
        group.MapPut("/{nodeId}", UpdateNodeAsync).WithName("UpdateAssetComponentStructureNode");
        group.MapDelete("/{nodeId}", DeleteNodeAsync).WithName("DeleteAssetComponentStructureNode");

        return group;
    }

    private static async Task<IResult> GetNodesAsync(
        string assetRegistrationId,
        IAssetComponentStructureRepository repository,
        IAssetComponentRepository componentRepository,
        IAssetRegistrationRepository assetRepository,
        CancellationToken cancellationToken)
    {
        if (await assetRepository.GetByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken) is null)
        {
            return AssetNotFound(assetRegistrationId);
        }

        var nodes = await repository.GetByAssetAsync(assetRegistrationId, cancellationToken);
        var components = await ComponentsByIdAsync(assetRegistrationId, componentRepository, cancellationToken);
        return Results.Ok(nodes.OrderBy(node => node.CreatedAt).Select(node => ToResponse(node, components)).ToList());
    }

    private static async Task<IResult> CreateNodeAsync(
        string assetRegistrationId,
        SaveComponentStructureNodeRequest request,
        IAssetComponentStructureRepository repository,
        IAssetComponentRepository componentRepository,
        IAssetRegistrationRepository assetRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        if (await assetRepository.GetByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken) is null)
        {
            return AssetNotFound(assetRegistrationId);
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var node = new AssetComponentStructureNode
        {
            AssetRegistrationId = assetRegistrationId,
            CreatedBy = currentUser?.UserName,
            CreatedAt = DateTime.UtcNow,
            ClientId = currentUser?.ClientId,
            TenantId = currentUser?.TenantId,
            Status = "Active",
            IsDeleted = false
        };

        var components = await ComponentsByIdAsync(assetRegistrationId, componentRepository, cancellationToken);
        var problem = await ApplyAsync(request, node, null, repository, components, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        await repository.CreateAsync(node, cancellationToken);
        await eventLogger.LogAsync(EntityType, node.Id, $"{assetRegistrationId} - {node.Name}", EventAction.Created, cancellationToken);

        return Results.Created(
            $"/api/asset-registrations/{assetRegistrationId}/component-structure/{node.Id}",
            ToResponse(node, components));
    }

    private static async Task<IResult> UpdateNodeAsync(
        string assetRegistrationId,
        string nodeId,
        SaveComponentStructureNodeRequest request,
        IAssetComponentStructureRepository repository,
        IAssetComponentRepository componentRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var node = MongoObjectId.IsValid(nodeId) ? await repository.GetByIdAsync(nodeId, cancellationToken) : null;
        if (node is null || node.IsDeleted || node.AssetRegistrationId != assetRegistrationId)
        {
            return NodeNotFound(nodeId);
        }

        var components = await ComponentsByIdAsync(assetRegistrationId, componentRepository, cancellationToken);
        var problem = await ApplyAsync(request, node, nodeId, repository, components, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        node.UpdatedBy = currentUserAccessor.GetCurrentUser()?.UserName;
        node.UpdatedAt = DateTime.UtcNow;

        if (!await repository.UpdateAsync(nodeId, node, cancellationToken))
        {
            return NodeNotFound(nodeId);
        }

        await eventLogger.LogAsync(EntityType, node.Id, $"{assetRegistrationId} - {node.Name}", EventAction.Updated, cancellationToken);
        return Results.Ok(ToResponse(node, components));
    }

    private static async Task<IResult> DeleteNodeAsync(
        string assetRegistrationId,
        string nodeId,
        IAssetComponentStructureRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var node = MongoObjectId.IsValid(nodeId) ? await repository.GetByIdAsync(nodeId, cancellationToken) : null;
        if (node is null || node.IsDeleted || node.AssetRegistrationId != assetRegistrationId)
        {
            return NodeNotFound(nodeId);
        }

        if (await repository.HasChildrenAsync(nodeId, cancellationToken))
        {
            return Results.Conflict(new { message = $"'{node.Name}' has structure nodes under it. Move or delete them first." });
        }

        if (!await repository.DeleteAsync(nodeId, cancellationToken))
        {
            return NodeNotFound(nodeId);
        }

        await eventLogger.LogAsync(EntityType, node.Id, $"{assetRegistrationId} - {node.Name}", EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    /// <summary>Validates the request and copies it onto the node; returns the problem, or null when it was applied.</summary>
    private static async Task<IResult?> ApplyAsync(
        SaveComponentStructureNodeRequest request,
        AssetComponentStructureNode node,
        string? nodeId,
        IAssetComponentStructureRepository repository,
        IReadOnlyDictionary<string, AssetComponent> components,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var name = request.Name?.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        var parentId = string.IsNullOrWhiteSpace(request.ParentNodeId) ? null : request.ParentNodeId.Trim();
        var linkedId = string.IsNullOrWhiteSpace(request.LinkedComponentId) ? null : request.LinkedComponentId.Trim();

        if (string.IsNullOrEmpty(name))
        {
            errors["Name"] = ["Structure name is required"];
        }
        else if (name.Length > MaxNameLength)
        {
            errors["Name"] = [$"Structure name must be at most {MaxNameLength} characters"];
        }

        if (description is { Length: > MaxDescriptionLength })
        {
            errors["Description"] = [$"Description must be at most {MaxDescriptionLength} characters"];
        }

        if (linkedId is not null && !components.ContainsKey(linkedId))
        {
            errors["LinkedComponentId"] = [$"Component '{linkedId}' was not found on this asset"];
        }

        if (parentId is not null)
        {
            var parent = MongoObjectId.IsValid(parentId) ? await repository.GetByIdAsync(parentId, cancellationToken) : null;
            if (parent is not { IsDeleted: false } || parent.AssetRegistrationId != node.AssetRegistrationId)
            {
                errors["ParentNodeId"] = [$"Parent node '{parentId}' was not found on this asset"];
            }
            else if (nodeId is not null && await IsSelfOrDescendantAsync(parentId, nodeId, repository, cancellationToken))
            {
                errors["ParentNodeId"] = ["A node cannot be placed under itself or one of its own child nodes"];
            }
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        node.Name = name!;
        node.Description = description;
        node.ParentNodeId = parentId;
        node.LinkedComponentId = linkedId;
        return null;
    }

    // Walks up from the proposed parent; reaching the node being moved means the move would create a loop.
    private static async Task<bool> IsSelfOrDescendantAsync(string parentId, string nodeId, IAssetComponentStructureRepository repository, CancellationToken cancellationToken)
    {
        var visited = new HashSet<string>();
        string? currentId = parentId;

        while (currentId is not null && visited.Add(currentId))
        {
            if (currentId == nodeId)
            {
                return true;
            }

            currentId = (await repository.GetByIdAsync(currentId, cancellationToken))?.ParentNodeId;
        }

        return false;
    }

    private static async Task<IReadOnlyDictionary<string, AssetComponent>> ComponentsByIdAsync(
        string assetRegistrationId,
        IAssetComponentRepository componentRepository,
        CancellationToken cancellationToken)
    {
        return (await componentRepository.GetByAssetAsync(assetRegistrationId, cancellationToken)).ToDictionary(component => component.Id);
    }

    private static ComponentStructureNodeResponse ToResponse(AssetComponentStructureNode node, IReadOnlyDictionary<string, AssetComponent> components)
    {
        var linked = node.LinkedComponentId is not null ? components.GetValueOrDefault(node.LinkedComponentId) : null;
        return new ComponentStructureNodeResponse(
            node.Id,
            node.AssetRegistrationId,
            node.ParentNodeId,
            node.Name,
            node.LinkedComponentId,
            linked?.ComponentName,
            linked?.ComponentTag,
            node.Description,
            node.CreatedAt);
    }

    private static IResult AssetNotFound(string assetRegistrationId)
    {
        return Results.NotFound(new { message = $"Asset registration '{assetRegistrationId}' was not found." });
    }

    private static IResult NodeNotFound(string nodeId)
    {
        return Results.NotFound(new { message = $"Structure node '{nodeId}' was not found on this asset." });
    }
}
