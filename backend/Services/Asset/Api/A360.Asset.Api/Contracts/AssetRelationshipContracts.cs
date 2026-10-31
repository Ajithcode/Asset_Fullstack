using A360.Asset.Domain.Entities;

namespace A360.Asset.Api.Contracts;

/// <summary>
/// Submit from the Related Assets popup: one entry per ticked main asset with the sub assets to link.
/// A main asset that already has a relationship record gets these sub assets merged into it.
/// </summary>
public sealed record SaveAssetRelationshipsRequest(
    IReadOnlyCollection<AssetRelationshipItemRequest>? Relationships,
    string? CreatedBy,
    string? ClientId,
    string? TenantId);

public sealed record AssetRelationshipItemRequest(
    string? MainAssetRegistrationId,
    IReadOnlyCollection<AssetRelationshipSubAssetRequest>? SubAssets);

/// <summary>Edit from the row's + action or the expanded row: replaces the sub assets of one main asset.</summary>
public sealed record UpdateAssetRelationshipRequest(
    IReadOnlyCollection<AssetRelationshipSubAssetRequest>? SubAssets);

/// <summary>Send AssetRegistrationId for a registered asset, or ComponentId for a component of the main asset.</summary>
/// <param name="RelationshipType">One of AssetRelationshipTypes.All; defaults to "Component" for a component and "Sub Asset" otherwise.</param>
public sealed record AssetRelationshipSubAssetRequest(
    string? AssetRegistrationId,
    string? ComponentId,
    string? RelationshipType);

public sealed record DeleteAssetRelationshipsRequest(IReadOnlyCollection<string>? Ids);

public static class AssetRelationshipTypes
{
    public const string ConnectedAsset = "Connected Asset";
    public const string SupportingAsset = "Supporting Asset";
    public const string Component = "Component";
    public const string SubAsset = "Sub Asset";

    public static IEnumerable<string> All => [ConnectedAsset, SupportingAsset, Component, SubAsset];

    public static bool IsValid(string? type) =>
        All.Any(value => string.Equals(value, type?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Stores the type with its standard spelling, e.g. "sub asset" as "Sub Asset".</summary>
    public static string Normalize(string? type, bool isComponent) =>
        All.FirstOrDefault(value => string.Equals(value, type?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? (isComponent ? Component : SubAsset);
}

internal static class AssetRelationshipAssets
{
    public static void CopyMain(AssetRelationship relationship, AssetRegistration asset)
    {
        relationship.MainAssetRegistrationId = asset.AssetRegistrationId;
        relationship.MainAssetName = asset.BasicInformation.AssetName;
        relationship.MainAssetTag = asset.BasicInformation.AssetTag;
        relationship.MainCategory = asset.Classification.Category;
        relationship.MainLocation = asset.LocationAssignment.Location.LocationPath;
    }

    public static AssetRelationshipSubAsset FromAsset(AssetRegistration asset, string? relationshipType)
    {
        return new AssetRelationshipSubAsset
        {
            AssetRegistrationId = asset.AssetRegistrationId,
            Name = asset.BasicInformation.AssetName ?? asset.AssetRegistrationId,
            Tag = asset.BasicInformation.AssetTag,
            RelationshipType = AssetRelationshipTypes.Normalize(relationshipType, isComponent: false),
            Category = asset.Classification.Category,
            Location = asset.LocationAssignment.Location.LocationPath
        };
    }

    /// <summary>A component sits inside the main asset, so it takes the main asset's category and location.</summary>
    public static AssetRelationshipSubAsset FromComponent(AssetComponent component, AssetRegistration mainAsset, string? relationshipType)
    {
        return new AssetRelationshipSubAsset
        {
            ComponentId = component.Id,
            Name = component.ComponentName,
            Tag = component.ComponentTag,
            RelationshipType = AssetRelationshipTypes.Normalize(relationshipType, isComponent: true),
            Category = mainAsset.Classification.Category,
            Location = mainAsset.LocationAssignment.Location.LocationPath
        };
    }

    /// <summary>The key that identifies a sub asset within one main asset, so the same one is not added twice.</summary>
    public static string Key(AssetRelationshipSubAsset subAsset) =>
        subAsset.ComponentId is { Length: > 0 } componentId ? $"component:{componentId}" : $"asset:{subAsset.AssetRegistrationId}";
}

public sealed record AssetRelationshipSubAssetResponse(
    string? AssetRegistrationId,
    string? ComponentId,
    string Name,
    string? Tag,
    string RelationshipType,
    string? Category,
    string? Location)
{
    public static AssetRelationshipSubAssetResponse FromEntity(AssetRelationshipSubAsset subAsset)
    {
        return new AssetRelationshipSubAssetResponse(
            subAsset.AssetRegistrationId,
            subAsset.ComponentId,
            subAsset.Name,
            subAsset.Tag,
            subAsset.RelationshipType,
            subAsset.Category,
            subAsset.Location);
    }
}

/// <param name="CreatedBy">Created By column.</param>
/// <param name="LastUpdated">Last Updated column: UpdatedAt, or CreatedAt when never edited.</param>
public sealed record AssetRelationshipResponse(
    string Id,
    string MainAssetRegistrationId,
    string? MainAssetName,
    string? MainAssetTag,
    string? MainCategory,
    string? MainLocation,
    int SubAssetCount,
    IReadOnlyCollection<AssetRelationshipSubAssetResponse> SubAssets,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    DateTime? LastUpdated,
    string? ClientId,
    string? TenantId)
{
    public static AssetRelationshipResponse FromEntity(AssetRelationship relationship)
    {
        return new AssetRelationshipResponse(
            relationship.Id,
            relationship.MainAssetRegistrationId,
            relationship.MainAssetName,
            relationship.MainAssetTag,
            relationship.MainCategory,
            relationship.MainLocation,
            relationship.SubAssets.Count,
            relationship.SubAssets.Select(AssetRelationshipSubAssetResponse.FromEntity).ToList(),
            relationship.CreatedBy,
            relationship.CreatedAt,
            relationship.UpdatedBy,
            relationship.UpdatedAt,
            relationship.UpdatedAt ?? relationship.CreatedAt,
            relationship.ClientId,
            relationship.TenantId);
    }
}

/// <param name="TotalSubAssetLinks">For the "2 main assets · 5 sub asset links" header.</param>
public sealed record AssetRelationshipListResponse(
    IReadOnlyCollection<AssetRelationshipResponse> Items,
    long TotalCount,
    long TotalSubAssetLinks,
    int Page,
    int PageSize);

/// <summary>
/// One section of the Related Assets popup: the ticked asset's header and the rows to show for it,
/// made of the sub assets already saved for it plus its components that are not linked yet.
/// </summary>
/// <param name="RelationshipId">The saved relationship record, or null when this asset has none yet.</param>
/// <param name="Status">The asset's status, e.g. "Active".</param>
public sealed record AssetRelatedAssetsResponse(
    string? RelationshipId,
    string MainAssetRegistrationId,
    string? MainAssetName,
    string? MainAssetTag,
    string? MainCategory,
    string? MainLocation,
    string? Status,
    int SubAssetCount,
    IReadOnlyCollection<AssetRelationshipSubAssetResponse> SubAssets);
