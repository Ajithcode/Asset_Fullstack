
using WorkflowBuilderEntity = A360.Workflows.Domain.Entities.WorkflowBuilder;

namespace A360.Workflows.Api.Contracts;

public sealed record CreateWorkflowBuilderRequest(
    string? WorkflowName,
    string? Module,
    string? TriggerEvent,
    string? Version,
    bool StartFromTemplate,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status,
    string? Design = null)
{
    public WorkflowBuilderEntity ToEntity()
    {
        return new WorkflowBuilderEntity
        {
            WorkflowName = WorkflowName,
            Module = Module,
            TriggerEvent = TriggerEvent,
            Version = Version,
            StartFromTemplate = StartFromTemplate,
            Design = Design,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateWorkflowBuilderRequest(
    string? WorkflowName,
    string? Module,
    string? TriggerEvent,
    string? Version,
    bool StartFromTemplate,
    string? UpdatedBy,
    string? Status,
    string? Design = null)
{
    public void ApplyTo(WorkflowBuilderEntity workflowBuilder)
    {
        workflowBuilder.WorkflowName = WorkflowName ?? workflowBuilder.WorkflowName;
        workflowBuilder.Module = Module;
        workflowBuilder.TriggerEvent = TriggerEvent;
        workflowBuilder.Version = Version;
        workflowBuilder.StartFromTemplate = StartFromTemplate;

        // Older clients send no design: keep the stored one
        if (Design is not null)
        {
            workflowBuilder.Design = Design;
        }

        workflowBuilder.UpdatedBy = UpdatedBy;
        workflowBuilder.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            workflowBuilder.Status = Status;
        }
    }
}

public sealed record WorkflowBuilderResponse(
    string Id,
    string WorkflowName,
    string? Module,
    string? TriggerEvent,
    string? Version,
    bool StartFromTemplate,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    bool IsDeleted,
    string? Design)
{
    public static WorkflowBuilderResponse FromEntity(WorkflowBuilderEntity workflowBuilder)
    {
        return new WorkflowBuilderResponse(
            workflowBuilder.Id ?? string.Empty,
            workflowBuilder.WorkflowName ?? string.Empty,
            workflowBuilder.Module,
            workflowBuilder.TriggerEvent,
            workflowBuilder.Version,
            workflowBuilder.StartFromTemplate,
            workflowBuilder.Status,

            workflowBuilder.CreatedBy,
            workflowBuilder.CreatedAt,
            workflowBuilder.UpdatedBy,
            workflowBuilder.UpdatedAt,

            workflowBuilder.ClientId,
            workflowBuilder.TenantId,
            workflowBuilder.IsDeleted,
            workflowBuilder.Design
        );
    }
}
