using ModuleAccessEntity = A360.MasterDataManagement.Domain.Entities.ModuleAccess;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateModuleAccessRequest(
    string? ModuleName,
    string? Code,
    string? Category,
    string? Description,
    string? Icon,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public ModuleAccessEntity ToEntity()
    {
        return new ModuleAccessEntity
        {
            ModuleName = ModuleName!.Trim(),
            Code = Code!.Trim(),
            Category = Category!.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Icon = string.IsNullOrWhiteSpace(Icon) ? null : Icon.Trim(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateModuleAccessRequest(
    string? ModuleName,
    string? Code,
    string? Category,
    string? Description,
    string? Icon,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(ModuleAccessEntity moduleAccess)
    {
        moduleAccess.ModuleName = ModuleName!.Trim();
        moduleAccess.Code = Code!.Trim();
        moduleAccess.Category = Category!.Trim();
        moduleAccess.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        moduleAccess.Icon = string.IsNullOrWhiteSpace(Icon) ? null : Icon.Trim();

        moduleAccess.UpdatedBy = UpdatedBy;
        moduleAccess.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            moduleAccess.Status = Status;
        }
    }
}

public sealed record UpdateModuleAccessStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record ModuleAccessResponse(
    string Id,
    string ModuleName,
    string Code,
    string Category,
    string? Description,
    string? Icon,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static ModuleAccessResponse FromEntity(ModuleAccessEntity moduleAccess)
    {
        return new ModuleAccessResponse(
            moduleAccess.Id ?? string.Empty,
            moduleAccess.ModuleName ?? string.Empty,
            moduleAccess.Code ?? string.Empty,
            moduleAccess.Category ?? string.Empty,
            moduleAccess.Description,
            moduleAccess.Icon,
            moduleAccess.CreatedBy,
            moduleAccess.CreatedAt,
            moduleAccess.UpdatedBy,
            moduleAccess.UpdatedAt,
            moduleAccess.ClientId,
            moduleAccess.TenantId,
            moduleAccess.Status,
            moduleAccess.IsDeleted
        );
    }
}
