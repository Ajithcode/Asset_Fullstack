using PermissionEntity = A360.MasterDataManagement.Domain.Entities.Permission;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreatePermissionRequest(
    string? PermissionName,
    string? Code,
    MasterDataReferenceDto? Module,
    string? Type,
    string? Description,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public PermissionEntity ToEntity()
    {
        return new PermissionEntity
        {
            PermissionName = PermissionName!.Trim(),
            Code = Code!.Trim(),
            Module = Module?.ToReference(),
            Type = Type!.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdatePermissionRequest(
    string? PermissionName,
    string? Code,
    MasterDataReferenceDto? Module,
    string? Type,
    string? Description,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(PermissionEntity permission)
    {
        permission.PermissionName = PermissionName!.Trim();
        permission.Code = Code!.Trim();
        permission.Module = Module?.ToReference();
        permission.Type = Type!.Trim();
        permission.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

        permission.UpdatedBy = UpdatedBy;
        permission.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            permission.Status = Status;
        }
    }
}

public sealed record UpdatePermissionStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record PermissionResponse(
    string Id,
    string PermissionName,
    string Code,
    MasterDataReferenceDto? Module,
    string Type,
    string? Description,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static PermissionResponse FromEntity(PermissionEntity permission)
    {
        return new PermissionResponse(
            permission.Id ?? string.Empty,
            permission.PermissionName ?? string.Empty,
            permission.Code ?? string.Empty,
            permission.Module is null
                ? null
                : MasterDataReferenceDto.FromReference(permission.Module),
            permission.Type ?? string.Empty,
            permission.Description,
            permission.CreatedBy,
            permission.CreatedAt,
            permission.UpdatedBy,
            permission.UpdatedAt,
            permission.ClientId,
            permission.TenantId,
            permission.Status,
            permission.IsDeleted
        );
    }
}
