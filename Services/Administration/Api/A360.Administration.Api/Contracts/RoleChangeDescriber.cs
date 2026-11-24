using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Contracts;

/// <summary>
/// What a role edit changed, for the System Audit Log. Only changed fields and permission-matrix entries are listed:
/// "Changed: Status: Active → Inactive; Assets › edit: none → full; Reports & Analytics › view: — → full".
/// "—" means the entry was not set. The Module Access page edits the same matrix, so its changes show here too.
/// </summary>
public static class RoleChangeDescriber
{
    public const string Prefix = "Changed: ";
    private const string Missing = "—";
    private const int MaxLength = 4_000;

    public sealed record Snapshot(
        string RoleName,
        string RoleCode,
        string? Description,
        string RoleType,
        string? Status,
        Dictionary<string, Dictionary<string, string>> Permissions);

    /// <summary>A copy of the role's fields and matrix taken before the edit is applied.</summary>
    public static Snapshot Capture(Role role) => new(
        role.RoleName,
        role.RoleCode,
        role.Description,
        role.RoleType,
        role.Status,
        (role.Permissions ?? []).ToDictionary(
            module => module.Key,
            module => new Dictionary<string, string>(module.Value ?? [], StringComparer.OrdinalIgnoreCase),
            StringComparer.Ordinal));

    /// <summary>The changes as one line, or null when nothing changed.</summary>
    public static string? Describe(Snapshot before, Role after)
    {
        var changes = new List<string>();
        AddField(changes, "Role name", before.RoleName, after.RoleName);
        AddField(changes, "Role code", before.RoleCode, after.RoleCode);
        AddField(changes, "Role type", before.RoleType, after.RoleType);
        AddField(changes, "Status", before.Status, after.Status);
        AddField(changes, "Description", Shorten(before.Description), Shorten(after.Description));

        var afterPermissions = after.Permissions ?? [];
        var modules = before.Permissions.Keys.Concat(afterPermissions.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(module => module, StringComparer.OrdinalIgnoreCase);
        foreach (var module in modules)
        {
            var old = before.Permissions.GetValueOrDefault(module) ?? [];
            var current = afterPermissions.TryGetValue(module, out var actions) && actions is not null
                ? new Dictionary<string, string>(actions, StringComparer.OrdinalIgnoreCase)
                : [];
            var names = old.Keys.Concat(current.Keys)
                .Select(action => action.ToLowerInvariant())
                .Distinct()
                .OrderBy(ActionOrder);
            foreach (var action in names)
            {
                var from = old.GetValueOrDefault(action) ?? Missing;
                var to = current.GetValueOrDefault(action) ?? Missing;
                if (!string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
                {
                    changes.Add($"{Clean(module)} › {action}: {from} → {to}");
                }
            }
        }

        if (changes.Count == 0)
        {
            return null;
        }

        var text = Prefix + string.Join("; ", changes);
        if (text.Length <= MaxLength)
        {
            return text;
        }

        // Keep whole entries and say how many were left out
        var kept = new List<string>();
        var length = Prefix.Length;
        foreach (var change in changes)
        {
            if (length + change.Length + 2 > MaxLength - 40)
            {
                break;
            }

            kept.Add(change);
            length += change.Length + 2;
        }

        return $"{Prefix}{string.Join("; ", kept)}; … and {changes.Count - kept.Count} more";
    }

    private static void AddField(List<string> changes, string label, string? from, string? to)
    {
        from = string.IsNullOrWhiteSpace(from) ? Missing : from.Trim();
        to = string.IsNullOrWhiteSpace(to) ? Missing : to.Trim();
        if (!string.Equals(from, to, StringComparison.Ordinal))
        {
            changes.Add($"{label}: {Clean(from)} → {Clean(to)}");
        }
    }

    private static readonly string[] KnownOrder = ["view", "create", "edit", "delete", "approve", "export"];

    private static int ActionOrder(string action)
    {
        var index = Array.IndexOf(KnownOrder, action);
        return index < 0 ? KnownOrder.Length : index;
    }

    private static string? Shorten(string? value) =>
        value is null ? null : value.Length <= 60 ? value : value[..57] + "...";

    // The separators "; " and " → " are kept out of values so the line can be read back into entries
    private static string Clean(string value) => value.Replace(";", ",").Replace("→", "->").Replace("\r", " ").Replace("\n", " ");
}
