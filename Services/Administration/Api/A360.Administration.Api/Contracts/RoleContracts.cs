using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Contracts;

public sealed record CreateRoleRequest(
    string? RoleName,
    string? RoleCode,
    string? Description,
    string? RoleType,
    string? Status,
    string? CreatedBy,
    string? TenantId,
    string? ClientId,
    Dictionary<string, Dictionary<string, string>>? Permissions);

// PUT /api/roles/{roleId}
public sealed record UpdateRoleRequest(
    string? RoleName,
    string? RoleCode,
    string? Description,
    string? RoleType,
    string? Status,
    string? UpdatedBy,
    Dictionary<string, Dictionary<string, string>>? Permissions);

public sealed record RoleResponse(
    string RoleId,
    string RoleName,
    string RoleCode,
    string? Description,
    string RoleType,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? TenantId,
    string? ClientId,
    IReadOnlyDictionary<string, Dictionary<string, string>> Permissions)
{
    public static RoleResponse FromEntity(Role role)
    {
        return new RoleResponse(
            role.RoleId,
            role.RoleName,
            role.RoleCode,
            role.Description,
            role.RoleType,
            role.Status,
            role.CreatedBy,
            role.CreatedAt,
            role.UpdatedBy,
            role.UpdatedAt,
            role.TenantId,
            role.ClientId,
            role.Permissions);
    }
}
