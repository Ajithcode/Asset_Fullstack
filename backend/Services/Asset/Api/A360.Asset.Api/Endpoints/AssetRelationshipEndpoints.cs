using A360.Asset.Api.Contracts;
using A360.Asset.Api.Validation;
using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;
using A360.Domain.Entities;
using A360.Repository.Activity;
using A360.Security;

namespace A360.Asset.Api.Endpoints;

/// <summary>Main assets and their linked sub assets, for the Asset Relationship screen.</summary>
/// <remarks>
/// The asset list at the top of the screen is GET /api/asset-registrations.
/// View History reads GET /api/eventlogs?entityType=AssetRelationship&amp;entityId={id}.
/// </remarks>
public static class AssetRelationshipEndpoints
{
    private const string EntityType = "AssetRelationship";

    public static RouteGroupBuilder MapAssetRelationshipEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/asset-relationships").WithTags("Asset Relationships");

        group.MapGet("", GetRelationshipsAsync).WithName("GetAssetRelationships");
        group.MapGet("/related", GetRelatedAssetsAsync).WithName("GetAssetRelatedAssets");
        group.MapGet("/types", () => Results.Ok(AssetRelationshipTypes.All)).WithName("GetAssetRelationshipTypes");
        group.MapGet("/{id}", GetRelationshipByIdAsync).WithName("GetAssetRelationshipById");
        group.MapPost("", SaveRelationshipsAsync).WithName("SaveAssetRelationships");
        group.MapPut("/{id}", UpdateRelationshipAsync).WithName("UpdateAssetRelationship");
        group.MapDelete("/{id}", DeleteRelationshipAsync).WithName("DeleteAssetRelationship");
        group.MapPost("/bulk-delete", DeleteRelationshipsAsync).WithName("DeleteAssetRelationships");

        return group;
    }

    private static async Task<IResult> GetRelationshipsAsync(
        IAssetRelationshipRepository repository,
        CancellationToken cancellationToken,
        string? search = null,
        string? assetRegistrationId = null,
        string? relationshipType = null,
        int page = 1,
        int pageSize = AssetRelationshipRepository.DefaultPageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > AssetRelationshipRepository.MaxPageSize ? AssetRelationshipRepository.DefaultPageSize : pageSize;

        var result = await repository.QueryAsync(
            new AssetRelationshipQuery(search, assetRegistrationId, relationshipType, page, pageSize),
            cancellationToken);

        return Results.Ok(new AssetRelationshipListResponse(
            result.Items.Select(AssetRelationshipResponse.FromEntity).ToList(),
            result.TotalCount,
            result.TotalSubAssetLinks,
            page,
            pageSize));
    }

    /// <summary>
    /// Related Assets popup for the ticked assets (?assetRegistrationIds=A&amp;assetRegistrationIds=B, or "A,B").
    /// Each section lists the sub assets already saved for that asset, then its components not linked yet.
    /// </summary>
    private static async Task<IResult> GetRelatedAssetsAsync(
        string[]? assetRegistrationIds,
        IAssetRelationshipRepository repository,
        IAssetRegistrationRepository assetRepository,
        IAssetComponentRepository componentRepository,
        CancellationToken cancellationToken)
    {
        var ids = ContractText.CleanList(
            (assetRegistrationIds ?? []).SelectMany(value => value.Split(',')),
            StringComparer.Ordinal);
        if (ids.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["AssetRegistrationIds"] = ["Select at least one asset"]
            });
        }

        var sections = new List<AssetRelatedAssetsResponse>();
        foreach (var assetRegistrationId in ids)
        {
            var asset = await assetRepository.GetByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken);
            if (asset is null)
            {
                return AssetNotFound("AssetRegistrationIds", assetRegistrationId);
            }

            var relationship = await repository.GetByMainAssetAsync(assetRegistrationId, cancellationToken);
            var subAssets = relationship?.SubAssets.ToList() ?? [];
            var linked = subAssets.Select(AssetRelationshipAssets.Key).ToHashSet(StringComparer.Ordinal);

            var components = await componentRepository.GetByAssetAsync(assetRegistrationId, cancellationToken);
            subAssets.AddRange(components
                .Where(component => !component.IsDeleted)
                .Select(component => AssetRelationshipAssets.FromComponent(component, asset, AssetRelationshipTypes.Component))
                .Where(subAsset => !linked.Contains(AssetRelationshipAssets.Key(subAsset))));

            sections.Add(new AssetRelatedAssetsResponse(
                relationship?.Id,
                asset.AssetRegistrationId,
                asset.BasicInformation.AssetName,
                asset.BasicInformation.AssetTag,
                asset.Classification.Category,
                asset.LocationAssignment.Location.LocationPath,
                asset.BasicInformation.Status,
                subAssets.Count,
                subAssets.Select(AssetRelationshipSubAssetResponse.FromEntity).ToList()));
        }

        return Results.Ok(sections);
    }

    private static async Task<IResult> GetRelationshipByIdAsync(
        string id,
        IAssetRelationshipRepository repository,
        CancellationToken cancellationToken)
    {
        var relationship = await repository.GetByIdAsync(id, cancellationToken);

        return relationship is null || relationship.IsDeleted
            ? RelationshipNotFound(id)
            : Results.Ok(AssetRelationshipResponse.FromEntity(relationship));
    }

    /// <summary>
    /// Submit from the Related Assets popup. Saves one record per main asset; when the asset already has one,
    /// the sent sub assets are added to it (a sub asset already linked takes the newly sent relationship type).
    /// </summary>
    private static async Task<IResult> SaveRelationshipsAsync(
        SaveAssetRelationshipsRequest request,
        IAssetRelationshipRepository repository,
        IAssetRegistrationRepository assetRepository,
        IAssetComponentRepository componentRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        // Resolve everything first so nothing is saved when one asset or component is missing.
        var resolved = new List<(AssetRegistration MainAsset, List<AssetRelationshipSubAsset> SubAssets)>();
        var index = 0;
        foreach (var item in request.Relationships!)
        {
            var mainAssetRegistrationId = item.MainAssetRegistrationId!.Trim();
            var mainAsset = await assetRepository.GetByAssetRegistrationIdAsync(mainAssetRegistrationId, cancellationToken);
            if (mainAsset is null)
            {
                return AssetNotFound($"Relationships[{index}].MainAssetRegistrationId", mainAssetRegistrationId);
            }

            var (subAssets, error) = await ResolveSubAssetsAsync(
                mainAsset, item.SubAssets!, $"Relationships[{index}].SubAssets", assetRepository, componentRepository, cancellationToken);
            if (error is not null)
            {
                return error;
            }

            resolved.Add((mainAsset, subAssets));
            index++;
        }

        var currentUser = currentUserAccessor.GetCurrentUser();
        var saved = new List<AssetRelationshipResponse>();

        foreach (var (mainAsset, subAssets) in resolved)
        {
            var relationship = await repository.GetByMainAssetAsync(mainAsset.AssetRegistrationId, cancellationToken);
            if (relationship is null)
            {
                relationship = new AssetRelationship
                {
                    SubAssets = subAssets,
                    CreatedBy = ContractText.FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
                    CreatedAt = DateTime.UtcNow,
                    ClientId = ContractText.FirstNonEmpty(request.ClientId, currentUser?.ClientId),
                    TenantId = ContractText.FirstNonEmpty(request.TenantId, currentUser?.TenantId),
                    IsDeleted = false
                };
                AssetRelationshipAssets.CopyMain(relationship, mainAsset);

                await repository.CreateAsync(relationship, cancellationToken);
                await eventLogger.LogAsync(EntityType, relationship.Id, Describe(relationship), EventAction.Created, cancellationToken);
            }
            else
            {
                var merged = relationship.SubAssets.ToDictionary(AssetRelationshipAssets.Key, StringComparer.Ordinal);
                foreach (var subAsset in subAssets)
                {
                    merged[AssetRelationshipAssets.Key(subAsset)] = subAsset;
                }

                AssetRelationshipAssets.CopyMain(relationship, mainAsset);
                relationship.SubAssets = merged.Values.ToList();
                relationship.UpdatedBy = currentUser?.UserName;
                relationship.UpdatedAt = DateTime.UtcNow;

                if (!await repository.UpdateAsync(relationship.Id, relationship, cancellationToken))
                {
                    return RelationshipNotFound(relationship.Id);
                }

                await eventLogger.LogAsync(EntityType, relationship.Id, Describe(relationship), EventAction.Updated, cancellationToken);
            }

            saved.Add(AssetRelationshipResponse.FromEntity(relationship));
        }

        return Results.Ok(saved);
    }

    /// <summary>Replaces the sub assets of one main asset.</summary>
    private static async Task<IResult> UpdateRelationshipAsync(
        string id,
        UpdateAssetRelationshipRequest request,
        IAssetRelationshipRepository repository,
        IAssetRegistrationRepository assetRepository,
        IAssetComponentRepository componentRepository,
        ICurrentUserAccessor currentUserAccessor,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var relationship = await repository.GetByIdAsync(id, cancellationToken);
        if (relationship is null || relationship.IsDeleted)
        {
            return RelationshipNotFound(id);
        }

        var validationErrors = request.Validate(relationship.MainAssetRegistrationId);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        var mainAsset = await assetRepository.GetByAssetRegistrationIdAsync(relationship.MainAssetRegistrationId, cancellationToken);
        if (mainAsset is null)
        {
            return AssetNotFound("MainAssetRegistrationId", relationship.MainAssetRegistrationId);
        }

        var (subAssets, error) = await ResolveSubAssetsAsync(
            mainAsset, request.SubAssets!, "SubAssets", assetRepository, componentRepository, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        AssetRelationshipAssets.CopyMain(relationship, mainAsset);
        relationship.SubAssets = subAssets;
        relationship.UpdatedBy = currentUserAccessor.GetCurrentUser()?.UserName;
        relationship.UpdatedAt = DateTime.UtcNow;

        if (!await repository.UpdateAsync(id, relationship, cancellationToken))
        {
            return RelationshipNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, relationship.Id, Describe(relationship), EventAction.Updated, cancellationToken);

        return Results.Ok(AssetRelationshipResponse.FromEntity(relationship));
    }

    private static async Task<IResult> DeleteRelationshipAsync(
        string id,
        IAssetRelationshipRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var relationship = await repository.GetByIdAsync(id, cancellationToken);
        if (relationship is null || relationship.IsDeleted || !await repository.DeleteAsync(id, cancellationToken))
        {
            return RelationshipNotFound(id);
        }

        await eventLogger.LogAsync(EntityType, relationship.Id, Describe(relationship), EventAction.Deleted, cancellationToken);
        return Results.NoContent();
    }

    /// <summary>Deletes the relationships ticked in the list. Ids that are not found are skipped.</summary>
    private static async Task<IResult> DeleteRelationshipsAsync(
        DeleteAssetRelationshipsRequest request,
        IAssetRelationshipRepository repository,
        IEventLogger eventLogger,
        CancellationToken cancellationToken)
    {
        var ids = ContractText.CleanList(request.Ids, StringComparer.Ordinal);
        if (ids.Count == 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Ids"] = ["Select at least one relationship"]
            });
        }

        var deleted = 0;
        foreach (var id in ids)
        {
            var relationship = await repository.GetByIdAsync(id, cancellationToken);
            if (relationship is null || relationship.IsDeleted || !await repository.DeleteAsync(id, cancellationToken))
            {
                continue;
            }

            await eventLogger.LogAsync(EntityType, relationship.Id, Describe(relationship), EventAction.Deleted, cancellationToken);
            deleted++;
        }

        return Results.Ok(new { deletedCount = deleted });
    }

    /// <summary>Loads each sent sub asset; a component must belong to the main asset. Duplicates keep the last one sent.</summary>
    private static async Task<(List<AssetRelationshipSubAsset> SubAssets, IResult? Error)> ResolveSubAssetsAsync(
        AssetRegistration mainAsset,
        IReadOnlyCollection<AssetRelationshipSubAssetRequest> requests,
        string field,
        IAssetRegistrationRepository assetRepository,
        IAssetComponentRepository componentRepository,
        CancellationToken cancellationToken)
    {
        var subAssets = new Dictionary<string, AssetRelationshipSubAsset>(StringComparer.Ordinal);

        foreach (var request in requests)
        {
            AssetRelationshipSubAsset subAsset;
            if (!string.IsNullOrWhiteSpace(request.ComponentId))
            {
                var componentId = request.ComponentId.Trim();
                var component = await componentRepository.GetByIdAsync(componentId, cancellationToken);
                if (component is null || component.IsDeleted || component.AssetRegistrationId != mainAsset.AssetRegistrationId)
                {
                    return ([], Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        [field] = [$"Component '{componentId}' was not found on asset '{mainAsset.AssetRegistrationId}'"]
                    }));
                }

                subAsset = AssetRelationshipAssets.FromComponent(component, mainAsset, request.RelationshipType);
            }
            else
            {
                var assetRegistrationId = request.AssetRegistrationId!.Trim();
                var asset = await assetRepository.GetByAssetRegistrationIdAsync(assetRegistrationId, cancellationToken);
                if (asset is null)
                {
                    return ([], AssetNotFound(field, assetRegistrationId));
                }

                subAsset = AssetRelationshipAssets.FromAsset(asset, request.RelationshipType);
            }

            subAssets[AssetRelationshipAssets.Key(subAsset)] = subAsset;
        }

        return (subAssets.Values.ToList(), null);
    }

    private static string Describe(AssetRelationship relationship) =>
        $"{relationship.MainAssetRegistrationId} - {relationship.MainAssetName} ({relationship.SubAssets.Count} sub assets)";

    private static IResult AssetNotFound(string field, string assetRegistrationId)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [field] = [$"Asset '{assetRegistrationId}' was not found"]
        });
    }

    private static IResult RelationshipNotFound(string id)
    {
        return Results.NotFound(new { message = $"Asset relationship '{id}' was not found." });
    }
}
