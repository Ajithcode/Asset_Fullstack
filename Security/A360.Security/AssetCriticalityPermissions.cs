namespace A360.Security;

/// <summary>
/// Permission module "AssetCriticality" (API path /api/asset-criticality). Its own module so that criticality can be
/// assessed and approved without create / edit rights on the Assets module.
/// </summary>
/// <remarks>
/// Permissions: View, Assess, Approve (Low / Medium / High), ApproveCritical (Critical and any lowering of an asset's
/// criticality) and Configure. A role that lists the module in Role &amp; Access Control gets exactly what it lists;
/// otherwise the defaults below apply by role name: User = View + Assess, Manager = + Approve,
/// Administrator = everything. Sign-in administrators (full access) have everything.
/// </remarks>
public static class AssetCriticalityPermissions
{
    public const string Module = "AssetCriticality";

    /// <summary>API paths that belong to this module (gateway routes asset-criticality-root / asset-criticality).</summary>
    public const string PathPrefix = "/api/asset-criticality";

    public const string View = "view";
    public const string Assess = "assess";
    public const string Approve = "approve";
    public const string ApproveCritical = "approvecritical";
    public const string Configure = "configure";

    public static readonly IReadOnlyList<string> All = [View, Assess, Approve, ApproveCritical, Configure];

    /// <summary>Any of these lets a write through the gateway; the Asset service then checks the exact permission.</summary>
    public static readonly IReadOnlyList<string> Writes = [Assess, Approve, ApproveCritical, Configure];

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RoleDefaults =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["User"] = [View, Assess],
            ["Manager"] = [View, Assess, Approve],
            ["Administrator"] = All
        };

    /// <param name="fullAccess">From GET /api/users/me.</param>
    /// <param name="moduleActions">Actions /api/users/me lists for this module, or null when no role configures it.</param>
    /// <param name="roleNames">The caller's role names (primary and secondary).</param>
    public static IReadOnlySet<string> Resolve(bool fullAccess, IEnumerable<string>? moduleActions, IEnumerable<string>? roleNames)
    {
        if (fullAccess)
        {
            return new HashSet<string>(All, StringComparer.OrdinalIgnoreCase);
        }

        if (moduleActions is not null)
        {
            return new HashSet<string>(moduleActions.Select(Normalize), StringComparer.OrdinalIgnoreCase);
        }

        var granted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roleNames ?? [])
        {
            if (RoleDefaults.TryGetValue(role.Trim(), out var defaults))
            {
                granted.UnionWith(defaults);
            }
        }

        return granted;
    }

    /// <summary>"AssetCriticality.ApproveCritical", "Approve Critical" or "approvecritical" → "approvecritical".</summary>
    public static string Normalize(string action)
    {
        var name = action.Trim();
        var dot = name.LastIndexOf('.');
        if (dot >= 0)
        {
            name = name[(dot + 1)..];
        }

        return name.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    }

    public static bool IsModulePath(string? path) =>
        path is not null
        && (path.Equals(PathPrefix, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(PathPrefix + "/", StringComparison.OrdinalIgnoreCase));
}
