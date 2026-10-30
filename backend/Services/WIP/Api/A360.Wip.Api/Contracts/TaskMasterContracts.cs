
using TaskMasterEntity = A360.Wip.Domain.Entities.TaskMaster;

namespace A360.Wip.Api.Contracts;

public sealed record CreateTaskMasterRequest(
    string? TaskId,
    string? AssetId,
    string? AssetName,
    string? JobId,
    string? TaskName,
    string? Description,
    int? SequenceOrder,
    string? AssignedTo,
    DateTime? PlannedStartTime,
    DateTime? PlannedEndTime,
    DateTime? ActualStartTime,
    DateTime? ActualEndTime,
    string? DependencyTaskId,
    string? ChecklistId,
    double? CompletionPercentage,
    string? Remarks,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public TaskMasterEntity ToEntity()
    {
        return new TaskMasterEntity
        {
            TaskId = TaskId,
            AssetId = AssetId,
            AssetName = AssetName,
            JobId = JobId,

            TaskName = TaskName,
            Description = Description,
            SequenceOrder = SequenceOrder,
            AssignedTo = AssignedTo,

            PlannedStartTime = PlannedStartTime,
            PlannedEndTime = PlannedEndTime,
            ActualStartTime = ActualStartTime,
            ActualEndTime = ActualEndTime,

            DependencyTaskId = DependencyTaskId,
            ChecklistId = ChecklistId,
            CompletionPercentage = CompletionPercentage,
            Remarks = Remarks,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateTaskMasterRequest(
    string? AssetId,
    string? AssetName,
    string? JobId,
    string? TaskName,
    string? Description,
    int? SequenceOrder,
    string? AssignedTo,
    DateTime? PlannedStartTime,
    DateTime? PlannedEndTime,
    DateTime? ActualStartTime,
    DateTime? ActualEndTime,
    string? DependencyTaskId,
    string? ChecklistId,
    double? CompletionPercentage,
    string? Remarks,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(TaskMasterEntity taskMaster)
    {
        taskMaster.AssetId = AssetId;
        taskMaster.AssetName = AssetName;
        taskMaster.JobId = JobId;

        taskMaster.TaskName = TaskName;
        taskMaster.Description = Description;
        taskMaster.SequenceOrder = SequenceOrder;
        taskMaster.AssignedTo = AssignedTo;

        taskMaster.PlannedStartTime = PlannedStartTime;
        taskMaster.PlannedEndTime = PlannedEndTime;
        taskMaster.ActualStartTime = ActualStartTime;
        taskMaster.ActualEndTime = ActualEndTime;

        taskMaster.DependencyTaskId = DependencyTaskId;
        taskMaster.ChecklistId = ChecklistId;
        taskMaster.CompletionPercentage = CompletionPercentage;
        taskMaster.Remarks = Remarks;

        taskMaster.UpdatedBy = UpdatedBy;
        taskMaster.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            taskMaster.Status = Status;
        }
    }
}

public sealed record TaskMasterResponse(
    string Id,
    string TaskId,
    string? AssetId,
    string? AssetName,
    string? JobId,
    string TaskName,
    string? Description,
    int? SequenceOrder,
    string? AssignedTo,
    DateTime? PlannedStartTime,
    DateTime? PlannedEndTime,
    DateTime? ActualStartTime,
    DateTime? ActualEndTime,
    string? DependencyTaskId,
    string? ChecklistId,
    double? CompletionPercentage,
    string? Remarks,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static TaskMasterResponse FromEntity(TaskMasterEntity taskMaster)
    {
        return new TaskMasterResponse(
            taskMaster.Id ?? string.Empty,
            taskMaster.TaskId ?? string.Empty,
            taskMaster.AssetId,
            taskMaster.AssetName,
            taskMaster.JobId,

            taskMaster.TaskName ?? string.Empty,
            taskMaster.Description,
            taskMaster.SequenceOrder,
            taskMaster.AssignedTo,

            taskMaster.PlannedStartTime,
            taskMaster.PlannedEndTime,
            taskMaster.ActualStartTime,
            taskMaster.ActualEndTime,

            taskMaster.DependencyTaskId,
            taskMaster.ChecklistId,
            taskMaster.CompletionPercentage,
            taskMaster.Remarks,

            taskMaster.CreatedBy,
            taskMaster.CreatedAt,
            taskMaster.UpdatedBy,
            taskMaster.UpdatedAt,

            taskMaster.ClientId,
            taskMaster.TenantId,
            taskMaster.Status,
            taskMaster.IsDeleted
        );
    }
}
