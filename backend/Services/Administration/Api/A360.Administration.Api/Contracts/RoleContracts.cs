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
    string? ClientId);

// RoleId, audit and tenant/client fields are fixed once the role is created.
public sealed record UpdateRoleRequest(
    string? RoleName,
    string? RoleCode,
    string? Description,
    string? RoleType,
    string? Status);

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
    string? ClientId)
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
            role.ClientId);
    }
}
