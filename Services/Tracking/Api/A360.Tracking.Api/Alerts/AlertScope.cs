using A360.Tracking.Domain.Entities;

namespace A360.Tracking.Api.Alerts;

/// <summary>Which assets an alert rule (or an outdoor geofence's asset groups) applies to.</summary>
public static class AlertScope
{
    /// <summary>
    /// "All assets" matches every asset; another group matches the asset's category, sub-category or type
    /// ("Forklifts" ~ "Forklift"). An asset with no classification matches when <paramref name="unknownMatches"/>.
    /// </summary>
    public static bool InGroup(string? group, IReadOnlyCollection<string> assetGroups, bool unknownMatches = true)
    {
        if (string.IsNullOrWhiteSpace(group) || group.Trim().StartsWith("All", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (assetGroups.Count == 0)
        {
            return unknownMatches;
        }

        static string Norm(string v) => v.Trim().TrimEnd('s', 'S').ToLowerInvariant();
        var wanted = Norm(group);
        return assetGroups.Any(g => Norm(g) == wanted || Norm(g).Contains(wanted) || wanted.Contains(Norm(g)));
    }

    /// <summary>The rule's specific assets (registration id, asset tag or name); none = every asset.</summary>
    public static bool InAssets(AlertRule rule, params string?[] ids) =>
        rule.AssetIds.Count == 0 || rule.AssetIds.Any(id => ids.Any(x => string.Equals(id, x, StringComparison.OrdinalIgnoreCase)));
}
