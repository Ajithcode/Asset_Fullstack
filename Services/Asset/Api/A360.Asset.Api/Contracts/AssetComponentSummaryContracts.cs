using A360.Asset.Domain.Entities;

namespace A360.Asset.Api.Contracts;

/// <summary>Counts shown above an asset's component list.</summary>
/// <param name="TopLevel">Components fitted directly to the asset.</param>
/// <param name="SubComponents">Components fitted inside another component.</param>
/// <param name="ByStatus">Components per status (e.g. "Active": 4); components with no status are counted under "Unknown".</param>
/// <param name="ReplacementOverdue">Expected replacement date has passed.</param>
/// <param name="ReplacementDueSoon">Expected replacement within the next <see cref="AssetComponentSummary.DueSoonDays"/> days.</param>
/// <param name="AverageLifecyclePercent">Average life used across components that have a rated life; null when none do.</param>
public sealed record AssetComponentCounts(
    int Total,
    int TopLevel,
    int SubComponents,
    IReadOnlyDictionary<string, int> ByStatus,
    int ReplacementOverdue,
    int ReplacementDueSoon,
    decimal? AverageLifecyclePercent);

/// <summary>All of an asset's components: counts, a flat list with full details, and the parent/child tree.</summary>
public sealed record AssetComponentSummaryResponse(
    AssetComponentCounts Counts,
    IReadOnlyList<AssetComponentResponse> Items,
    IReadOnlyList<AssetComponentNodeResponse> Tree);

public static class AssetComponentSummary
{
    public const int DueSoonDays = 90;

    public static AssetComponentSummaryResponse From(IReadOnlyCollection<AssetComponent> components, DateTime? now = null)
    {
        var active = components.Where(component => !component.IsDeleted).ToList();
        var today = (now ?? DateTime.UtcNow).Date;
        var namesById = active.ToDictionary(component => component.Id, component => component.ComponentName);
        var childCounts = active
            .Where(component => component.ParentComponentId is not null)
            .GroupBy(component => component.ParentComponentId!)
            .ToDictionary(group => group.Key, group => group.Count());

        // A component whose parent is gone is shown at the top level.
        bool IsTopLevel(AssetComponent component) =>
            component.ParentComponentId is null || !namesById.ContainsKey(component.ParentComponentId);

        var lifecyclePercents = active
            .Select(AssetComponentResponse.LifecyclePercentOf)
            .Where(percent => percent is not null)
            .Select(percent => percent!.Value)
            .ToList();

        var counts = new AssetComponentCounts(
            active.Count,
            active.Count(IsTopLevel),
            active.Count(component => !IsTopLevel(component)),
            active
                .GroupBy(component => string.IsNullOrWhiteSpace(component.Status) ? "Unknown" : component.Status.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count()),
            active.Count(component => component.ExpectedReplacementDate?.Date < today),
            active.Count(component => component.ExpectedReplacementDate?.Date >= today
                && component.ExpectedReplacementDate.Value.Date <= today.AddDays(DueSoonDays)),
            lifecyclePercents.Count == 0
                ? null
                : decimal.Round(lifecyclePercents.Average(), 1, MidpointRounding.AwayFromZero));

        var items = active
            .OrderBy(component => component.ComponentName, StringComparer.OrdinalIgnoreCase)
            .Select(component => AssetComponentResponse.FromEntity(
                component,
                component.ParentComponentId is not null ? namesById.GetValueOrDefault(component.ParentComponentId) : null,
                childCounts.GetValueOrDefault(component.Id)))
            .ToList();

        return new AssetComponentSummaryResponse(counts, items, BuildTree(active));
    }

    /// <summary>Components nested under their parents; a component whose parent is missing is placed at the top.</summary>
    public static IReadOnlyList<AssetComponentNodeResponse> BuildTree(IReadOnlyCollection<AssetComponent> components)
    {
        var ids = components.Select(component => component.Id).ToHashSet(StringComparer.Ordinal);
        var childrenByParent = components.ToLookup(component =>
            component.ParentComponentId is not null && ids.Contains(component.ParentComponentId) ? component.ParentComponentId : string.Empty);

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

        return Build(string.Empty, []);
    }
}

/// <summary>The asset's own details for the relationship view.</summary>
public sealed record AssetRelationshipAssetDetails(
    string AssetRegistrationId,
    string? AssetName,
    string? AssetTag,
    string? ImageUrl,
    string? Status,
    string? Condition,
    string? Criticality,
    string? Category,
    string? SubCategory,
    string? AssetType,
    string? Department,
    string? Model,
    string? Manufacturer,
    string? SerialNumber,
    string? Description)
{
    public static AssetRelationshipAssetDetails FromEntity(AssetRegistration asset)
    {
        var basic = asset.BasicInformation;
        var classification = asset.Classification;
        return new AssetRelationshipAssetDetails(
            asset.AssetRegistrationId,
            basic.AssetName,
            basic.AssetTag,
            basic.Images.FirstOrDefault(),
            basic.Status,
            basic.Condition,
            classification.Criticality ?? basic.Criticality,
            classification.Category,
            classification.SubCategory,
            classification.AssetType,
            classification.Department,
            basic.Model,
            basic.Manufacturer ?? asset.PurchaseWarranty.Purchase.Manufacturer,
            basic.SerialNumber,
            basic.Description);
    }
}

/// <summary>
/// Everything the Asset Relationship view shows for one asset: its details, its location and assignment (as saved
/// in the registration's Location &amp; Assignment step), its components, and the other assets linked to it.
/// </summary>
/// <param name="RelationshipId">Id of the saved relationship record, for editing links; null when none is saved.</param>
/// <param name="LinkedAssets">Assets linked under this one in Asset Relationships (components are in <paramref name="Components"/>).</param>
/// <param name="ParentAssets">Assets that list this one as a sub asset.</param>
public sealed record AssetRelationshipDetailsResponse(
    AssetRelationshipAssetDetails Asset,
    AssetLocationDto Location,
    AssetAssignmentDto Assignment,
    AssetComponentSummaryResponse Components,
    string? RelationshipId,
    IReadOnlyList<AssetRelationshipSubAssetResponse> LinkedAssets,
    IReadOnlyList<AssetRelationshipParentResponse> ParentAssets);

public sealed record AssetRelationshipParentResponse(
    string? RelationshipId,
    string AssetRegistrationId,
    string? AssetName,
    string? AssetTag,
    string RelationshipType,
    string? Category,
    string? Location);
