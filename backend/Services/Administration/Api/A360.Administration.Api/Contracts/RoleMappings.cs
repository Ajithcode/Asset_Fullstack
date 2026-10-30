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
            IsDeleted = false
        };
    }

    private static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }
}
