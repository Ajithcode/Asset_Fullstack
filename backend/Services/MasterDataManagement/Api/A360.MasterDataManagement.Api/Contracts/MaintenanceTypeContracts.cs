using MaintenanceTypeEntity = A360.MasterDataManagement.Domain.Entities.MaintenanceType;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateMaintenanceTypeRequest(
    string? MaintenanceTypeName,
    string? Code,
    string? Description,
    string? Color,
    bool? Planned,
    bool? ApprovalRequired,
    bool? CountsAsDowntime,
    string? DefaultPriority,
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
            Planned = Planned ?? false,
            ApprovalRequired = ApprovalRequired ?? false,
            CountsAsDowntime = CountsAsDowntime ?? false,
            DefaultPriority = string.IsNullOrWhiteSpace(DefaultPriority) ? null : DefaultPriority.Trim(),

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
    bool? Planned,
    bool? ApprovalRequired,
    bool? CountsAsDowntime,
    string? DefaultPriority,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(MaintenanceTypeEntity maintenanceType)
    {
        maintenanceType.MaintenanceTypeName = MaintenanceTypeName!.Trim();
        maintenanceType.Code = Code!.Trim();
        maintenanceType.Description = Description!.Trim();
        maintenanceType.Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant();
        maintenanceType.Planned = Planned ?? false;
        maintenanceType.ApprovalRequired = ApprovalRequired ?? false;
        maintenanceType.CountsAsDowntime = CountsAsDowntime ?? false;
        maintenanceType.DefaultPriority = string.IsNullOrWhiteSpace(DefaultPriority) ? null : DefaultPriority.Trim();

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
    bool Planned,
    bool ApprovalRequired,
    bool CountsAsDowntime,
    string? DefaultPriority,
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
            maintenanceType.Planned,
            maintenanceType.ApprovalRequired,
            maintenanceType.CountsAsDowntime,
            maintenanceType.DefaultPriority,
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
