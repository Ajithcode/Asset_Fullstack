
using WorkflowApprovalTaskEntity = A360.Workflows.Domain.Entities.WorkflowApprovalTask;

namespace A360.Workflows.Api.Contracts;

public sealed record CreateWorkflowApprovalTaskRequest(
    string? WorkflowName,
    string? AssetRequest,
    string? RequestedBy,
    string? AssignedTo,
    DateTime? DueDate,
    string? Priority,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public WorkflowApprovalTaskEntity ToEntity()
    {
        return new WorkflowApprovalTaskEntity
        {
            WorkflowName = WorkflowName,
            AssetRequest = AssetRequest,
            RequestedBy = RequestedBy,
            AssignedTo = AssignedTo,
            DueDate = DueDate,
            Priority = Priority,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateWorkflowApprovalTaskRequest(
    string? WorkflowName,
    string? AssetRequest,
    string? RequestedBy,
    string? AssignedTo,
    DateTime? DueDate,
    string? Priority,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(WorkflowApprovalTaskEntity workflowApprovalTask)
    {
        workflowApprovalTask.WorkflowName = WorkflowName ?? workflowApprovalTask.WorkflowName;
        workflowApprovalTask.AssetRequest = AssetRequest;
        workflowApprovalTask.RequestedBy = RequestedBy;
        workflowApprovalTask.AssignedTo = AssignedTo;
        workflowApprovalTask.DueDate = DueDate;
        workflowApprovalTask.Priority = Priority;

        workflowApprovalTask.UpdatedBy = UpdatedBy;
        workflowApprovalTask.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            workflowApprovalTask.Status = Status;
        }
    }
}

public sealed record WorkflowApprovalTaskResponse(
    string Id,
    string WorkflowName,
    string? AssetRequest,
    string? RequestedBy,
    string? AssignedTo,
    DateTime? DueDate,
    string? Priority,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    bool IsDeleted)
{
    public static WorkflowApprovalTaskResponse FromEntity(WorkflowApprovalTaskEntity workflowApprovalTask)
    {
        return new WorkflowApprovalTaskResponse(
            workflowApprovalTask.Id ?? string.Empty,
            workflowApprovalTask.WorkflowName ?? string.Empty,
            workflowApprovalTask.AssetRequest,
            workflowApprovalTask.RequestedBy,
            workflowApprovalTask.AssignedTo,
            workflowApprovalTask.DueDate,
            workflowApprovalTask.Priority,
            workflowApprovalTask.Status,

            workflowApprovalTask.CreatedBy,
            workflowApprovalTask.CreatedAt,
            workflowApprovalTask.UpdatedBy,
            workflowApprovalTask.UpdatedAt,

            workflowApprovalTask.ClientId,
            workflowApprovalTask.TenantId,
            workflowApprovalTask.IsDeleted
        );
    }
}
