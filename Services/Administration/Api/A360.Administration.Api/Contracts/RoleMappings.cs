using A360.Administration.Domain.Entities;
using A360.Security;

namespace A360.Administration.Api.Contracts;

internal static class RoleMappings
{
    public static Role ToEntity(this CreateRoleRequest request, CurrentUser? currentUser)
    {
        return new Role
        {
            // Generated on the server so every role gets a stable id independent of the Mongo _id.
            RoleId = Guid.NewGuid().ToString(),
            RoleName = request.RoleName!.Trim(),
            RoleCode = request.RoleCode!.Trim().ToUpperInvariant(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            RoleType = request.RoleType!.Trim(),
            Status = request.Status!.Trim(),
            CreatedBy = FirstNonEmpty(request.CreatedBy, currentUser?.UserName),
            CreatedAt = DateTime.UtcNow,
            TenantId = FirstNonEmpty(request.TenantId, currentUser?.TenantId),
            ClientId = FirstNonEmpty(request.ClientId, currentUser?.ClientId),
            Permissions = CleanPermissions(request.Permissions),
            IsDeleted = false
        };
    }

    public static void ApplyTo(this UpdateRoleRequest request, Role role, CurrentUser? currentUser)
    {
        role.RoleName = request.RoleName!.Trim();
        role.RoleCode = request.RoleCode!.Trim().ToUpperInvariant();
        role.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        role.RoleType = request.RoleType!.Trim();
        role.Status = request.Status!.Trim();
        // Leave the matrix alone when the request doesn't carry one
        if (request.Permissions is not null)
        {
            role.Permissions = CleanPermissions(request.Permissions);
        }
        role.UpdatedBy = FirstNonEmpty(request.UpdatedBy, currentUser?.UserName);
        role.UpdatedAt = DateTime.UtcNow;
    }

    private static readonly string[] Actions = ["view", "create", "edit", "delete", "approve", "export"];
    private static readonly string[] States = ["full", "partial", "none", "na"];

    /// <summary>Keeps known actions and states only; module names are trimmed. Mongo field names can't contain '.' or start with '$'.</summary>
    private static Dictionary<string, Dictionary<string, string>> CleanPermissions(Dictionary<string, Dictionary<string, string>>? permissions)
    {
        var clean = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        if (permissions is null)
        {
            return clean;
        }

        foreach (var (module, actions) in permissions)
        {
            var name = module?.Trim();
            if (string.IsNullOrEmpty(name) || name.Contains('.') || name.StartsWith('$') || actions is null)
            {
                continue;
            }

            clean[name] = actions
                .Where(a => Actions.Contains(a.Key, StringComparer.OrdinalIgnoreCase) && States.Contains(a.Value, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(a => a.Key.ToLowerInvariant(), a => a.Value.ToLowerInvariant());
        }

        return clean;
    }

    private static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }
}
