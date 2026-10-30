
using WorkOrderEntity = A360.Maintenance.Domain.Entities.WorkOrder;

namespace A360.Maintenance.Api.Contracts;

public sealed record CreateWorkOrderRequest(
    string? WorkOrderId,
    string? AssetId,
    string? AssetName,
    string? WorkType,
    string? Title,
    string? Description,
    string? Priority,
    DateTime? ScheduledDate,
    string? AssignedTechnician,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public WorkOrderEntity ToEntity()
    {
        return new WorkOrderEntity
        {
            WorkOrderId = WorkOrderId,
            AssetId = AssetId,
            AssetName = AssetName,
            WorkType = WorkType,

            Title = Title,
            Description = Description,

            Priority = Priority,
            ScheduledDate = ScheduledDate,
            AssignedTechnician = AssignedTechnician,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateWorkOrderRequest(
    string? AssetId,
    string? AssetName,
    string? WorkType,
    string? Title,
    string? Description,
    string? Priority,
    DateTime? ScheduledDate,
    string? AssignedTechnician,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(WorkOrderEntity workOrder)
    {
        workOrder.AssetId = AssetId;
        workOrder.AssetName = AssetName;
        workOrder.WorkType = WorkType;

        workOrder.Title = Title;
        workOrder.Description = Description;

        workOrder.Priority = Priority;
        workOrder.ScheduledDate = ScheduledDate;
        workOrder.AssignedTechnician = AssignedTechnician;

        workOrder.UpdatedBy = UpdatedBy;
        workOrder.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            workOrder.Status = Status;
        }
    }
}

public sealed record WorkOrderResponse(
    string Id,
    string WorkOrderId,
    string AssetId,
    string AssetName,
    string WorkType,
    string Title,
    string? Description,
    string Priority,
    DateTime? ScheduledDate,
    string? AssignedTechnician,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static WorkOrderResponse FromEntity(WorkOrderEntity workOrder)
    {
        return new WorkOrderResponse(
            workOrder.Id ?? string.Empty,
            workOrder.WorkOrderId ?? string.Empty,
            workOrder.AssetId ?? string.Empty,
            workOrder.AssetName ?? string.Empty,
            workOrder.WorkType ?? string.Empty,

            workOrder.Title ?? string.Empty,
            workOrder.Description,

            workOrder.Priority ?? string.Empty,
            workOrder.ScheduledDate,
            workOrder.AssignedTechnician,

            workOrder.CreatedBy,
            workOrder.CreatedAt,
            workOrder.UpdatedBy,
            workOrder.UpdatedAt,

            workOrder.ClientId,
            workOrder.TenantId,
            workOrder.Status,
            workOrder.IsDeleted
        );
    }
}
