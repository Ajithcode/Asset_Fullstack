using PreventiveMaintenanceEntity = A360.Maintenance.Domain.Entities.PreventiveMaintenance;

namespace A360.Maintenance.Api.Contracts;

/// <summary>Editable PM schedule fields shared by create and update.</summary>
public abstract record PreventiveMaintenanceFields
{
    public string? ScheduleName { get; init; }
    public string? AssetId { get; init; }
    public string? AssetName { get; init; }
    public string? AssetType { get; init; }
    public string? Location { get; init; }
    public string? Frequency { get; init; }
    public string? TriggerType { get; init; }
    public DateTime? LastMaintenanceDate { get; init; }
    public DateTime? NextDueDate { get; init; }
    public bool AutoCreateWorkOrder { get; init; }
    public string? Priority { get; init; }
    public string? AssignedTo { get; init; }
    public double? EstimatedHours { get; init; }
    public string? Description { get; init; }
    public List<string>? Checklist { get; init; }
    public string? Status { get; init; }

    protected void CopyTo(PreventiveMaintenanceEntity schedule)
    {
        schedule.ScheduleName = ScheduleName;
        schedule.AssetId = AssetId!;
        schedule.AssetName = AssetName ?? string.Empty;
        schedule.AssetType = AssetType;
        schedule.Location = Location;
        schedule.Frequency = Frequency!;
        schedule.TriggerType = TriggerType!;
        schedule.LastMaintenanceDate = LastMaintenanceDate;
        schedule.NextDueDate = NextDueDate;
        schedule.AutoCreateWorkOrder = AutoCreateWorkOrder;
        schedule.Priority = Priority;
        schedule.AssignedTo = AssignedTo;
        schedule.EstimatedHours = EstimatedHours;
        schedule.Description = Description;
        schedule.Checklist = Checklist ?? schedule.Checklist;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            schedule.Status = Status;
        }
    }
}

public sealed record CreatePreventiveMaintenanceRequest : PreventiveMaintenanceFields
{
    public string? PmScheduleId { get; init; }
    public string? CreatedBy { get; init; }
    public string? ClientId { get; init; }
    public string? TenantId { get; init; }

    public PreventiveMaintenanceEntity ToEntity()
    {
        var schedule = new PreventiveMaintenanceEntity
        {
            PmScheduleId = PmScheduleId!,
            Status = "Active",
            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        CopyTo(schedule);
        return schedule;
    }
}

public sealed record UpdatePreventiveMaintenanceRequest : PreventiveMaintenanceFields
{
    public string? UpdatedBy { get; init; }

    public void ApplyTo(PreventiveMaintenanceEntity schedule)
    {
        CopyTo(schedule);
        schedule.UpdatedBy = UpdatedBy;
        schedule.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record PreventiveMaintenanceResponse
{
    public string Id { get; init; } = string.Empty;
    public string PmScheduleId { get; init; } = string.Empty;
    public string? ScheduleName { get; init; }
    public string AssetId { get; init; } = string.Empty;
    public string AssetName { get; init; } = string.Empty;
    public string? AssetType { get; init; }
    public string? Location { get; init; }
    public string Frequency { get; init; } = string.Empty;
    public string TriggerType { get; init; } = string.Empty;
    public DateTime? LastMaintenanceDate { get; init; }
    public DateTime? NextDueDate { get; init; }
    public bool AutoCreateWorkOrder { get; init; }
    public string? Priority { get; init; }
    public string? AssignedTo { get; init; }
    public double? EstimatedHours { get; init; }
    public string? Description { get; init; }
    public List<string> Checklist { get; init; } = [];
    public string? CreatedBy { get; init; }
    public DateTime? CreatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? ClientId { get; init; }
    public string? TenantId { get; init; }
    public string? Status { get; init; }
    public bool IsDeleted { get; init; }

    public static PreventiveMaintenanceResponse FromEntity(PreventiveMaintenanceEntity schedule) => new()
    {
        Id = schedule.Id ?? string.Empty,
        PmScheduleId = schedule.PmScheduleId ?? string.Empty,
        ScheduleName = schedule.ScheduleName,
        AssetId = schedule.AssetId ?? string.Empty,
        AssetName = schedule.AssetName ?? string.Empty,
        AssetType = schedule.AssetType,
        Location = schedule.Location,
        Frequency = schedule.Frequency ?? string.Empty,
        TriggerType = schedule.TriggerType ?? string.Empty,
        LastMaintenanceDate = schedule.LastMaintenanceDate,
        NextDueDate = schedule.NextDueDate,
        AutoCreateWorkOrder = schedule.AutoCreateWorkOrder,
        Priority = schedule.Priority,
        AssignedTo = schedule.AssignedTo,
        EstimatedHours = schedule.EstimatedHours,
        Description = schedule.Description,
        Checklist = schedule.Checklist ?? [],
        CreatedBy = schedule.CreatedBy,
        CreatedAt = schedule.CreatedAt,
        UpdatedBy = schedule.UpdatedBy,
        UpdatedAt = schedule.UpdatedAt,
        ClientId = schedule.ClientId,
        TenantId = schedule.TenantId,
        Status = schedule.Status,
        IsDeleted = schedule.IsDeleted
    };
}
