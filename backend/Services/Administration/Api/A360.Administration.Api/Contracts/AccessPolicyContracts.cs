using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Contracts;

public sealed record CreateAccessPolicyRequest(
    string? PolicyName,
    string? PolicyCode,
    string? Description,
    string? PolicyType,
    string? Status,
    List<string>? AssignedRoles,
    string? CreatedBy,
    string? TenantId,
    string? ClientId);

public sealed record AssignedRoleResponse(
    string RoleId,
    string RoleCode,
    string RoleName);

public sealed record AccessPolicyResponse(
    string PolicyId,
    string PolicyName,
    string PolicyCode,
    string? Description,
    string PolicyType,
    string? Status,
    IReadOnlyCollection<AssignedRoleResponse> AssignedRoles,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? TenantId,
    string? ClientId)
{
    // Roles that no longer exist are left out rather than failing the whole response.
    public static AccessPolicyResponse FromEntity(AccessPolicy policy, IReadOnlyDictionary<string, Role> rolesById)
    {
        return new AccessPolicyResponse(
            policy.PolicyId,
            policy.PolicyName,
            policy.PolicyCode,
            policy.Description,
            policy.PolicyType,
            policy.Status,
            policy.AssignedRoleIds
                .Where(rolesById.ContainsKey)
                .Select(roleId => rolesById[roleId])
                .Select(role => new AssignedRoleResponse(role.RoleId, role.RoleCode, role.RoleName))
                .ToList(),
            policy.CreatedBy,
            policy.CreatedAt,
            policy.UpdatedBy,
            policy.UpdatedAt,
            policy.TenantId,
            policy.ClientId);
    }
}
