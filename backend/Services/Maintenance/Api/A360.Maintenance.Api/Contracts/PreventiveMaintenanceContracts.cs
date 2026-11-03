
using PreventiveMaintenanceEntity = A360.Maintenance.Domain.Entities.PreventiveMaintenance;

namespace A360.Maintenance.Api.Contracts;

public sealed record CreatePreventiveMaintenanceRequest(
    string? PmScheduleId,
    string? AssetId,
    string? AssetName,
    string? Frequency,
    string? TriggerType,
    DateTime? LastMaintenanceDate,
    DateTime? NextDueDate,
    bool AutoCreateWorkOrder,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public PreventiveMaintenanceEntity ToEntity()
    {
        return new PreventiveMaintenanceEntity
        {
            PmScheduleId = PmScheduleId,
            AssetId = AssetId,
            AssetName = AssetName,

            Frequency = Frequency,
            TriggerType = TriggerType,

            LastMaintenanceDate = LastMaintenanceDate,
            NextDueDate = NextDueDate,
            AutoCreateWorkOrder = AutoCreateWorkOrder,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdatePreventiveMaintenanceRequest(
    string? AssetId,
    string? AssetName,
    string? Frequency,
    string? TriggerType,
    DateTime? LastMaintenanceDate,
    DateTime? NextDueDate,
    bool AutoCreateWorkOrder,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(PreventiveMaintenanceEntity preventiveMaintenance)
    {
        preventiveMaintenance.AssetId = AssetId;
        preventiveMaintenance.AssetName = AssetName;

        preventiveMaintenance.Frequency = Frequency;
        preventiveMaintenance.TriggerType = TriggerType;

        preventiveMaintenance.LastMaintenanceDate = LastMaintenanceDate;
        preventiveMaintenance.NextDueDate = NextDueDate;
        preventiveMaintenance.AutoCreateWorkOrder = AutoCreateWorkOrder;

        preventiveMaintenance.UpdatedBy = UpdatedBy;
        preventiveMaintenance.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            preventiveMaintenance.Status = Status;
        }
    }
}

public sealed record PreventiveMaintenanceResponse(
    string Id,
    string PmScheduleId,
    string AssetId,
    string AssetName,
    string Frequency,
    string TriggerType,
    DateTime? LastMaintenanceDate,
    DateTime? NextDueDate,
    bool AutoCreateWorkOrder,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static PreventiveMaintenanceResponse FromEntity(PreventiveMaintenanceEntity preventiveMaintenance)
    {
        return new PreventiveMaintenanceResponse(
            preventiveMaintenance.Id ?? string.Empty,
            preventiveMaintenance.PmScheduleId ?? string.Empty,
            preventiveMaintenance.AssetId ?? string.Empty,
            preventiveMaintenance.AssetName ?? string.Empty,

            preventiveMaintenance.Frequency ?? string.Empty,
            preventiveMaintenance.TriggerType ?? string.Empty,

            preventiveMaintenance.LastMaintenanceDate,
            preventiveMaintenance.NextDueDate,
            preventiveMaintenance.AutoCreateWorkOrder,

            preventiveMaintenance.CreatedBy,
            preventiveMaintenance.CreatedAt,
            preventiveMaintenance.UpdatedBy,
            preventiveMaintenance.UpdatedAt,

            preventiveMaintenance.ClientId,
            preventiveMaintenance.TenantId,
            preventiveMaintenance.Status,
            preventiveMaintenance.IsDeleted
        );
    }
}
