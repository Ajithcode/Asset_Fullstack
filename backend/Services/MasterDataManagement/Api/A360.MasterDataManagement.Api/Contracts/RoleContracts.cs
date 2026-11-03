using RoleEntity = A360.MasterDataManagement.Domain.Entities.Role;

namespace A360.MasterDataManagement.Api.Contracts;

// CopyFromRoleId fills Permissions and ModuleAccess from an existing role when those lists are left out.
public sealed record CreateRoleRequest(
    string? RoleName,
    string? Code,
    string? Type,
    string? Description,
    List<MasterDataReferenceDto>? Permissions,
    List<MasterDataReferenceDto>? ModuleAccess,
    string? CopyFromRoleId,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public RoleEntity ToEntity()
    {
        return new RoleEntity
        {
            RoleName = RoleName!.Trim(),
            Code = Code!.Trim(),
            Type = Type!.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Permissions = Permissions?.Select(x => x.ToReference()).ToList() ?? [],
            ModuleAccess = ModuleAccess?.Select(x => x.ToReference()).ToList() ?? [],

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateRoleRequest(
    string? RoleName,
    string? Code,
    string? Type,
    string? Description,
    List<MasterDataReferenceDto>? Permissions,
    List<MasterDataReferenceDto>? ModuleAccess,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(RoleEntity role)
    {
        role.RoleName = RoleName!.Trim();
        role.Code = Code!.Trim();
        role.Type = Type!.Trim();
        role.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        role.Permissions = Permissions?.Select(x => x.ToReference()).ToList() ?? [];
        role.ModuleAccess = ModuleAccess?.Select(x => x.ToReference()).ToList() ?? [];

        role.UpdatedBy = UpdatedBy;
        role.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            role.Status = Status;
        }
    }
}

public sealed record UpdateRoleStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record RoleResponse(
    string Id,
    string RoleName,
    string Code,
    string Type,
    string? Description,
    IReadOnlyList<MasterDataReferenceDto> Permissions,
    IReadOnlyList<MasterDataReferenceDto> ModuleAccess,
    int UserCount,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static RoleResponse FromEntity(
        RoleEntity role,
        int userCount)
    {
        return new RoleResponse(
            role.Id ?? string.Empty,
            role.RoleName ?? string.Empty,
            role.Code ?? string.Empty,
            role.Type ?? string.Empty,
            role.Description,
            (role.Permissions ?? []).Select(MasterDataReferenceDto.FromReference).ToList(),
            (role.ModuleAccess ?? []).Select(MasterDataReferenceDto.FromReference).ToList(),
            userCount,
            role.CreatedBy,
            role.CreatedAt,
            role.UpdatedBy,
            role.UpdatedAt,
            role.ClientId,
            role.TenantId,
            role.Status,
            role.IsDeleted
        );
    }
}
