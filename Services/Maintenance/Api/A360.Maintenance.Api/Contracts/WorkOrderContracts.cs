using A360.Maintenance.Domain.Entities;
using WorkOrderEntity = A360.Maintenance.Domain.Entities.WorkOrder;

namespace A360.Maintenance.Api.Contracts;

/// <summary>Editable work order fields shared by create and update.</summary>
public abstract record WorkOrderFields
{
    public string? AssetId { get; init; }
    public string? AssetName { get; init; }
    public string? WorkType { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Priority { get; init; }
    public DateTime? ScheduledDate { get; init; }
    public string? AssignedTechnician { get; init; }
    public string? Category { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? PlannedStartDate { get; init; }
    public DateTime? PlannedEndDate { get; init; }
    public DateTime? ActualStartDate { get; init; }
    public DateTime? ActualEndDate { get; init; }
    public double? EstimatedHours { get; init; }
    public string? Source { get; init; }
    public string? SourceReference { get; init; }
    public string? Site { get; init; }
    public string? Building { get; init; }
    public string? Area { get; init; }
    public string? Department { get; init; }
    public string? Team { get; init; }
    public string? Vendor { get; init; }
    public string? Notes { get; init; }
    public int? Progress { get; init; }
    public List<WorkOrderTask>? Tasks { get; init; }
    public List<WorkOrderPart>? Parts { get; init; }
    public List<WorkOrderLabour>? Labour { get; init; }
    public double? AdditionalCost { get; init; }
    public List<WorkOrderDocument>? Documents { get; init; }
    public string? Status { get; init; }

    protected void CopyTo(WorkOrderEntity workOrder)
    {
        workOrder.AssetId = AssetId!;
        workOrder.AssetName = AssetName ?? string.Empty;
        workOrder.WorkType = WorkType!;
        workOrder.Title = Title!;
        workOrder.Description = Description;
        workOrder.Priority = Priority!;
        workOrder.ScheduledDate = ScheduledDate;
        workOrder.AssignedTechnician = AssignedTechnician;
        workOrder.Category = Category;
        workOrder.DueDate = DueDate;
        workOrder.PlannedStartDate = PlannedStartDate;
        workOrder.PlannedEndDate = PlannedEndDate;
        workOrder.ActualStartDate = ActualStartDate;
        workOrder.ActualEndDate = ActualEndDate;
        workOrder.EstimatedHours = EstimatedHours;
        workOrder.Source = Source ?? workOrder.Source;
        workOrder.SourceReference = SourceReference ?? workOrder.SourceReference;
        workOrder.Site = Site;
        workOrder.Building = Building;
        workOrder.Area = Area;
        workOrder.Department = Department;
        workOrder.Team = Team;
        workOrder.Vendor = Vendor;
        workOrder.Notes = Notes;
        workOrder.Tasks = Tasks ?? workOrder.Tasks;
        workOrder.Parts = Parts ?? workOrder.Parts;
        workOrder.Labour = Labour ?? workOrder.Labour;
        workOrder.AdditionalCost = AdditionalCost ?? workOrder.AdditionalCost;
        workOrder.Documents = Documents ?? workOrder.Documents;
        workOrder.Progress = Progress ?? workOrder.Progress;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            workOrder.Status = Status;
        }

        ApplyWorkflowRules(workOrder);
    }

    /// <summary>
    /// Keeps derived fields consistent: progress follows the task list, starting work stamps the actual
    /// start, and completing stamps the actual end and sets progress to 100.
    /// </summary>
    private static void ApplyWorkflowRules(WorkOrderEntity workOrder)
    {
        var now = DateTime.UtcNow;

        foreach (var task in workOrder.Tasks)
        {
            task.CompletedAt = task.IsDone ? task.CompletedAt ?? now : null;
        }

        if (workOrder.Tasks.Count > 0)
        {
            var done = workOrder.Tasks.Count(task => task.IsDone);
            workOrder.Progress = (int)Math.Round(done * 100.0 / workOrder.Tasks.Count);
        }

        if (workOrder.Status == "In Progress")
        {
            workOrder.ActualStartDate ??= now;
        }

        if (workOrder.Status == "Completed")
        {
            workOrder.ActualStartDate ??= now;
            workOrder.ActualEndDate ??= now;
            workOrder.Progress = 100;
        }

        workOrder.Progress = Math.Clamp(workOrder.Progress, 0, 100);
    }
}

public sealed record CreateWorkOrderRequest : WorkOrderFields
{
    public string? WorkOrderId { get; init; }
    public string? CreatedBy { get; init; }
    public string? ClientId { get; init; }
    public string? TenantId { get; init; }

    public WorkOrderEntity ToEntity()
    {
        var now = DateTime.UtcNow;
        var workOrder = new WorkOrderEntity
        {
            WorkOrderId = WorkOrderId!,
            Source = "Manual",
            Status = "Open",
            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            CreatedAt = now,
            IsDeleted = false
        };

        CopyTo(workOrder);

        workOrder.History.Add(new WorkOrderHistoryEntry
        {
            At = now,
            By = CreatedBy,
            Action = "Created",
            Details = string.IsNullOrWhiteSpace(workOrder.SourceReference)
                ? $"Work order created with status {workOrder.Status}."
                : $"Work order created from {workOrder.Source} {workOrder.SourceReference}."
        });

        return workOrder;
    }
}

public sealed record UpdateWorkOrderRequest : WorkOrderFields
{
    public string? UpdatedBy { get; init; }

    public void ApplyTo(WorkOrderEntity workOrder)
    {
        var previousStatus = workOrder.Status;
        var previousAssignee = workOrder.AssignedTechnician;

        CopyTo(workOrder);

        workOrder.UpdatedBy = UpdatedBy;
        workOrder.UpdatedAt = DateTime.UtcNow;

        var changes = new List<string>();
        if (previousStatus != workOrder.Status)
        {
            changes.Add($"Status changed from {previousStatus ?? "—"} to {workOrder.Status}.");
        }

        if (previousAssignee != workOrder.AssignedTechnician)
        {
            changes.Add($"Assigned to {workOrder.AssignedTechnician ?? "nobody"}.");
        }

        workOrder.History.Add(new WorkOrderHistoryEntry
        {
            At = workOrder.UpdatedAt.Value,
            By = UpdatedBy,
            Action = previousStatus != workOrder.Status ? "Status Changed" : "Updated",
            Details = changes.Count > 0 ? string.Join(" ", changes) : "Work order details updated."
        });
    }
}

public sealed record WorkOrderResponse
{
    public string Id { get; init; } = string.Empty;
    public string WorkOrderId { get; init; } = string.Empty;
    public string AssetId { get; init; } = string.Empty;
    public string AssetName { get; init; } = string.Empty;
    public string WorkType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Priority { get; init; } = string.Empty;
    public DateTime? ScheduledDate { get; init; }
    public string? AssignedTechnician { get; init; }
    public string? Category { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? PlannedStartDate { get; init; }
    public DateTime? PlannedEndDate { get; init; }
    public DateTime? ActualStartDate { get; init; }
    public DateTime? ActualEndDate { get; init; }
    public double? EstimatedHours { get; init; }
    public string? Source { get; init; }
    public string? SourceReference { get; init; }
    public string? Site { get; init; }
    public string? Building { get; init; }
    public string? Area { get; init; }
    public string? Department { get; init; }
    public string? Team { get; init; }
    public string? Vendor { get; init; }
    public string? Notes { get; init; }
    public int Progress { get; init; }
    public List<WorkOrderTask> Tasks { get; init; } = [];
    public List<WorkOrderPart> Parts { get; init; } = [];
    public List<WorkOrderLabour> Labour { get; init; } = [];
    public double AdditionalCost { get; init; }
    public List<WorkOrderDocument> Documents { get; init; } = [];
    public List<WorkOrderHistoryEntry> History { get; init; } = [];
    public double PartsCost { get; init; }
    public double LabourCost { get; init; }
    public double TotalCost { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? CreatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? ClientId { get; init; }
    public string? TenantId { get; init; }
    public string? Status { get; init; }
    public bool IsDeleted { get; init; }

    public static WorkOrderResponse FromEntity(WorkOrderEntity workOrder)
    {
        var partsCost = workOrder.Parts.Sum(part => part.Quantity * part.UnitCost);
        var labourCost = workOrder.Labour.Sum(labour => labour.Hours * labour.HourlyRate);

        return new WorkOrderResponse
        {
            Id = workOrder.Id ?? string.Empty,
            WorkOrderId = workOrder.WorkOrderId ?? string.Empty,
            AssetId = workOrder.AssetId ?? string.Empty,
            AssetName = workOrder.AssetName ?? string.Empty,
            WorkType = workOrder.WorkType ?? string.Empty,
            Title = workOrder.Title ?? string.Empty,
            Description = workOrder.Description,
            Priority = workOrder.Priority ?? string.Empty,
            ScheduledDate = workOrder.ScheduledDate,
            AssignedTechnician = workOrder.AssignedTechnician,
            Category = workOrder.Category,
            DueDate = workOrder.DueDate,
            PlannedStartDate = workOrder.PlannedStartDate,
            PlannedEndDate = workOrder.PlannedEndDate,
            ActualStartDate = workOrder.ActualStartDate,
            ActualEndDate = workOrder.ActualEndDate,
            EstimatedHours = workOrder.EstimatedHours,
            Source = workOrder.Source,
            SourceReference = workOrder.SourceReference,
            Site = workOrder.Site,
            Building = workOrder.Building,
            Area = workOrder.Area,
            Department = workOrder.Department,
            Team = workOrder.Team,
            Vendor = workOrder.Vendor,
            Notes = workOrder.Notes,
            Progress = workOrder.Progress,
            Tasks = workOrder.Tasks ?? [],
            Parts = workOrder.Parts ?? [],
            Labour = workOrder.Labour ?? [],
            AdditionalCost = workOrder.AdditionalCost,
            Documents = workOrder.Documents ?? [],
            History = (workOrder.History ?? []).OrderByDescending(entry => entry.At).ToList(),
            PartsCost = partsCost,
            LabourCost = labourCost,
            TotalCost = partsCost + labourCost + workOrder.AdditionalCost,
            CreatedBy = workOrder.CreatedBy,
            CreatedAt = workOrder.CreatedAt,
            UpdatedBy = workOrder.UpdatedBy,
            UpdatedAt = workOrder.UpdatedAt,
            ClientId = workOrder.ClientId,
            TenantId = workOrder.TenantId,
            Status = workOrder.Status,
            IsDeleted = workOrder.IsDeleted
        };
    }
}
