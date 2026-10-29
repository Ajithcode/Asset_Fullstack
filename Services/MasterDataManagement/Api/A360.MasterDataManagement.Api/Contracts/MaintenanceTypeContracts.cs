using MaintenanceTypeEntity = A360.MasterDataManagement.Domain.Entities.MaintenanceType;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateMaintenanceTypeRequest(
    string? MaintenanceTypeName,
    string? Code,
    string? Description,
    string? Color,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public MaintenanceTypeEntity ToEntity()
    {
        return new MaintenanceTypeEntity
        {
            MaintenanceTypeName = MaintenanceTypeName!.Trim(),
            Code = Code!.Trim(),
            Description = Description!.Trim(),
            Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateMaintenanceTypeRequest(
    string? MaintenanceTypeName,
    string? Code,
    string? Description,
    string? Color,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(MaintenanceTypeEntity maintenanceType)
    {
        maintenanceType.MaintenanceTypeName = MaintenanceTypeName!.Trim();
        maintenanceType.Code = Code!.Trim();
        maintenanceType.Description = Description!.Trim();
        maintenanceType.Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant();

        maintenanceType.UpdatedBy = UpdatedBy;
        maintenanceType.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            maintenanceType.Status = Status;
        }
    }
}

public sealed record UpdateMaintenanceTypeStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record MaintenanceTypeResponse(
    string Id,
    string MaintenanceTypeName,
    string Code,
    string Description,
    string? Color,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static MaintenanceTypeResponse FromEntity(MaintenanceTypeEntity maintenanceType)
    {
        return new MaintenanceTypeResponse(
            maintenanceType.Id ?? string.Empty,
            maintenanceType.MaintenanceTypeName ?? string.Empty,
            maintenanceType.Code ?? string.Empty,
            maintenanceType.Description ?? string.Empty,
            maintenanceType.Color,
            maintenanceType.CreatedBy,
            maintenanceType.CreatedAt,
            maintenanceType.UpdatedBy,
            maintenanceType.UpdatedAt,
            maintenanceType.ClientId,
            maintenanceType.TenantId,
            maintenanceType.Status,
            maintenanceType.IsDeleted
        );
    }
}
