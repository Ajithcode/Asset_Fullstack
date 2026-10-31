using InspectionTaskEntity = A360.Inspection.Domain.Entities.InspectionTask;

namespace A360.Inspection.Api.Contracts;

public sealed record CreateInspectionTaskRequest(
    string? AssetId,
    string? AssetName,
    string? TaskTitle,
    string? TaskCategory,
    string? TaskDescription,
    string? ResponseType,
    bool? IsCriticalTask,
    bool? IsMandatory,
    bool? IsActive,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public InspectionTaskEntity ToEntity(string taskCode)
    {
        return new InspectionTaskEntity
        {
            TaskCode = taskCode,
            AssetId = AssetId ?? string.Empty,
            AssetName = AssetName ?? string.Empty,
            TaskTitle = TaskTitle ?? string.Empty,
            TaskCategory = TaskCategory ?? string.Empty,
            TaskDescription = TaskDescription ?? string.Empty,
            ResponseType = ResponseType ?? string.Empty,
            IsCriticalTask = IsCriticalTask ?? false,
            IsMandatory = IsMandatory ?? false,
            IsActive = IsActive ?? true,
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };
    }
}

public sealed record UpdateInspectionTaskRequest(
    string? AssetId,
    string? AssetName,
    string? TaskTitle,
    string? TaskCategory,
    string? TaskDescription,
    string? ResponseType,
    bool? IsCriticalTask,
    bool? IsMandatory,
    bool? IsActive,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(InspectionTaskEntity task)
    {
        task.AssetId = AssetId ?? string.Empty;
        task.AssetName = AssetName ?? string.Empty;
        task.TaskTitle = TaskTitle ?? string.Empty;
        task.TaskCategory = TaskCategory ?? string.Empty;
        task.TaskDescription = TaskDescription ?? string.Empty;
        task.ResponseType = ResponseType ?? string.Empty;
        task.IsCriticalTask = IsCriticalTask ?? task.IsCriticalTask;
        task.IsMandatory = IsMandatory ?? task.IsMandatory;
        task.IsActive = IsActive ?? task.IsActive;
        task.Status = Status;
        task.UpdatedBy = UpdatedBy;
        task.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record InspectionTaskResponse(
    string Id,
    string TaskCode,
    string AssetId,
    string AssetName,
    string TaskTitle,
    string TaskCategory,
    string TaskDescription,
    string ResponseType,
    bool IsCriticalTask,
    bool IsMandatory,
    bool IsActive,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    bool IsDeleted)
{
    public static InspectionTaskResponse FromEntity(InspectionTaskEntity task)
    {
        return new InspectionTaskResponse(
            task.Id,
            task.TaskCode,
            task.AssetId,
            task.AssetName,
            task.TaskTitle,
            task.TaskCategory,
            task.TaskDescription,
            task.ResponseType,
            task.IsCriticalTask,
            task.IsMandatory,
            task.IsActive,
            task.Status,
            task.CreatedBy,
            task.CreatedAt,
            task.UpdatedBy,
            task.UpdatedAt,
            task.ClientId,
            task.TenantId,
            task.IsDeleted);
    }
}
