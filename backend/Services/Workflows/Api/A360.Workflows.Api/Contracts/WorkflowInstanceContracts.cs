
using WorkflowInstanceEntity = A360.Workflows.Domain.Entities.WorkflowInstance;

namespace A360.Workflows.Api.Contracts;

public sealed record CreateWorkflowInstanceRequest(
    string? WorkflowName,
    string? RelatedAssetRequest,
    string? RequestedBy,
    string? CurrentStep,
    DateTime? StartedOn,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public WorkflowInstanceEntity ToEntity()
    {
        return new WorkflowInstanceEntity
        {
            WorkflowName = WorkflowName,
            RelatedAssetRequest = RelatedAssetRequest,
            RequestedBy = RequestedBy,
            CurrentStep = CurrentStep,
            StartedOn = StartedOn ?? DateTime.UtcNow,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateWorkflowInstanceRequest(
    string? WorkflowName,
    string? RelatedAssetRequest,
    string? RequestedBy,
    string? CurrentStep,
    DateTime? StartedOn,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(WorkflowInstanceEntity workflowInstance)
    {
        workflowInstance.WorkflowName = WorkflowName ?? workflowInstance.WorkflowName;
        workflowInstance.RelatedAssetRequest = RelatedAssetRequest;
        workflowInstance.RequestedBy = RequestedBy;
        workflowInstance.CurrentStep = CurrentStep;
        workflowInstance.StartedOn = StartedOn ?? workflowInstance.StartedOn;

        workflowInstance.UpdatedBy = UpdatedBy;
        workflowInstance.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            workflowInstance.Status = Status;
        }
    }
}

public sealed record WorkflowInstanceResponse(
    string Id,
    string WorkflowName,
    string? RelatedAssetRequest,
    string? RequestedBy,
    string? CurrentStep,
    DateTime? StartedOn,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    bool IsDeleted)
{
    public static WorkflowInstanceResponse FromEntity(WorkflowInstanceEntity workflowInstance)
    {
        return new WorkflowInstanceResponse(
            workflowInstance.Id ?? string.Empty,
            workflowInstance.WorkflowName ?? string.Empty,
            workflowInstance.RelatedAssetRequest,
            workflowInstance.RequestedBy,
            workflowInstance.CurrentStep,
            workflowInstance.StartedOn,
            workflowInstance.Status,

            workflowInstance.CreatedBy,
            workflowInstance.CreatedAt,
            workflowInstance.UpdatedBy,
            workflowInstance.UpdatedAt,

            workflowInstance.ClientId,
            workflowInstance.TenantId,
            workflowInstance.IsDeleted
        );
    }
}
