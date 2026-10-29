using A360.Administration.Domain.Entities;
using A360.Security;

namespace A360.Administration.Api.Contracts;

internal static class AccessPolicyMappings
{
    public static AccessPolicy ToEntity(this CreateAccessPolicyRequest request, CurrentUser? currentUser)
    {
        return new AccessPolicy
        {
            // Generated on the server so every policy gets a stable id independent of the Mongo _id.
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = request.PolicyName!.Trim(),
            PolicyCode = request.PolicyCode!.Trim().ToUpperInvariant(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            PolicyType = request.PolicyType!.Trim(),
            Status = request.Status!.Trim(),
            AssignedRoleIds = request.AssignedRoles?
                .Where(roleId => !string.IsNullOrWhiteSpace(roleId))
                .Select(roleId => roleId.Trim())
                .Distinct()
                .ToList() ?? [],
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
