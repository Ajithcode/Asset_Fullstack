
using WorkflowListEntity = A360.Workflows.Domain.Entities.WorkflowList;

namespace A360.Workflows.Api.Contracts;

public sealed record CreateWorkflowListRequest(
    string? WorkflowName,
    string? Module,
    string? TriggerEvent,
    string? Version,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public WorkflowListEntity ToEntity()
    {
        return new WorkflowListEntity
        {
            WorkflowName = WorkflowName,
            Module = Module,
            TriggerEvent = TriggerEvent,
            Version = Version,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateWorkflowListRequest(
    string? WorkflowName,
    string? Module,
    string? TriggerEvent,
    string? Version,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(WorkflowListEntity workflowList)
    {
        workflowList.WorkflowName = WorkflowName ?? workflowList.WorkflowName;
        workflowList.Module = Module;
        workflowList.TriggerEvent = TriggerEvent;
        workflowList.Version = Version;

        workflowList.UpdatedBy = UpdatedBy;
        workflowList.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            workflowList.Status = Status;
        }
    }
}

public sealed record WorkflowListResponse(
    string Id,
    string WorkflowName,
    string? Module,
    string? TriggerEvent,
    string? Version,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    bool IsDeleted)
{
    public static WorkflowListResponse FromEntity(WorkflowListEntity workflowList)
    {
        return new WorkflowListResponse(
            workflowList.Id ?? string.Empty,
            workflowList.WorkflowName ?? string.Empty,
            workflowList.Module,
            workflowList.TriggerEvent,
            workflowList.Version,
            workflowList.Status,

            workflowList.CreatedBy,
            workflowList.CreatedAt,
            workflowList.UpdatedBy,
            workflowList.UpdatedAt,

            workflowList.ClientId,
            workflowList.TenantId,
            workflowList.IsDeleted
        );
    }
}
