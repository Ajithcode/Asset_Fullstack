
using MaintenanceTaskEntity = A360.Maintenance.Domain.Entities.MaintenanceTask;

namespace A360.Maintenance.Api.Contracts;

public sealed record CreateMaintenanceTaskRequest(
    string? AssetId,
    string? AssetName,
    List<string>? TaskChecklist,
    string? Instructions,
    List<string>? ToolsRequired,
    List<string>? SafetyProcedures,
    string? EstimatedDuration,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public MaintenanceTaskEntity ToEntity()
    {
        return new MaintenanceTaskEntity
        {
            AssetId = AssetId,
            AssetName = AssetName,

            TaskChecklist = TaskChecklist ?? new List<string>(),
            Instructions = Instructions,
            ToolsRequired = ToolsRequired ?? new List<string>(),
            SafetyProcedures = SafetyProcedures ?? new List<string>(),
            EstimatedDuration = EstimatedDuration,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateMaintenanceTaskRequest(
    string? AssetId,
    string? AssetName,
    List<string>? TaskChecklist,
    string? Instructions,
    List<string>? ToolsRequired,
    List<string>? SafetyProcedures,
    string? EstimatedDuration,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(MaintenanceTaskEntity maintenanceTask)
    {
        maintenanceTask.AssetId = AssetId;
        maintenanceTask.AssetName = AssetName;

        maintenanceTask.TaskChecklist = TaskChecklist ?? new List<string>();
        maintenanceTask.Instructions = Instructions;
        maintenanceTask.ToolsRequired = ToolsRequired ?? new List<string>();
        maintenanceTask.SafetyProcedures = SafetyProcedures ?? new List<string>();
        maintenanceTask.EstimatedDuration = EstimatedDuration;

        maintenanceTask.UpdatedBy = UpdatedBy;
        maintenanceTask.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            maintenanceTask.Status = Status;
        }
    }
}

public sealed record MaintenanceTaskResponse(
    string Id,
    string AssetId,
    string AssetName,
    List<string>? TaskChecklist,
    string? Instructions,
    List<string>? ToolsRequired,
    List<string>? SafetyProcedures,
    string? EstimatedDuration,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static MaintenanceTaskResponse FromEntity(MaintenanceTaskEntity maintenanceTask)
    {
        return new MaintenanceTaskResponse(
            maintenanceTask.Id ?? string.Empty,
            maintenanceTask.AssetId ?? string.Empty,
            maintenanceTask.AssetName ?? string.Empty,

            maintenanceTask.TaskChecklist,
            maintenanceTask.Instructions,
            maintenanceTask.ToolsRequired,
            maintenanceTask.SafetyProcedures,
            maintenanceTask.EstimatedDuration,

            maintenanceTask.CreatedBy,
            maintenanceTask.CreatedAt,
            maintenanceTask.UpdatedBy,
            maintenanceTask.UpdatedAt,

            maintenanceTask.ClientId,
            maintenanceTask.TenantId,
            maintenanceTask.Status,
            maintenanceTask.IsDeleted
        );
    }
}
