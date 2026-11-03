
using WorkflowInsightEntity = A360.Workflows.Domain.Entities.WorkflowInsight;

namespace A360.Workflows.Api.Contracts;

public sealed record CreateWorkflowInsightRequest(
    string? WorkflowName,
    string? Module,
    int PendingApprovals,
    int TotalInstances,
    double AvgApprovalTimeHrs,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public WorkflowInsightEntity ToEntity()
    {
        return new WorkflowInsightEntity
        {
            WorkflowName = WorkflowName,
            Module = Module,
            PendingApprovals = PendingApprovals,
            TotalInstances = TotalInstances,
            AvgApprovalTimeHrs = AvgApprovalTimeHrs,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateWorkflowInsightRequest(
    string? WorkflowName,
    string? Module,
    int PendingApprovals,
    int TotalInstances,
    double AvgApprovalTimeHrs,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(WorkflowInsightEntity workflowInsight)
    {
        workflowInsight.WorkflowName = WorkflowName ?? workflowInsight.WorkflowName;
        workflowInsight.Module = Module;
        workflowInsight.PendingApprovals = PendingApprovals;
        workflowInsight.TotalInstances = TotalInstances;
        workflowInsight.AvgApprovalTimeHrs = AvgApprovalTimeHrs;

        workflowInsight.UpdatedBy = UpdatedBy;
        workflowInsight.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            workflowInsight.Status = Status;
        }
    }
}

public sealed record WorkflowInsightResponse(
    string Id,
    string WorkflowName,
    string? Module,
    int PendingApprovals,
    int TotalInstances,
    double AvgApprovalTimeHrs,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    bool IsDeleted)
{
    public static WorkflowInsightResponse FromEntity(WorkflowInsightEntity workflowInsight)
    {
        return new WorkflowInsightResponse(
            workflowInsight.Id ?? string.Empty,
            workflowInsight.WorkflowName ?? string.Empty,
            workflowInsight.Module,
            workflowInsight.PendingApprovals,
            workflowInsight.TotalInstances,
            workflowInsight.AvgApprovalTimeHrs,
            workflowInsight.Status,

            workflowInsight.CreatedBy,
            workflowInsight.CreatedAt,
            workflowInsight.UpdatedBy,
            workflowInsight.UpdatedAt,

            workflowInsight.ClientId,
            workflowInsight.TenantId,
            workflowInsight.IsDeleted
        );
    }
}
