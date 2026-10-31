using A360.Inspection.Domain.Entities;
using InspectionTaskEntity = A360.Inspection.Domain.Entities.InspectionTask;

namespace A360.Inspection.Api.Contracts;

/// <summary>Editable fields shared by the create and update requests for InspectionTask.</summary>
public abstract record InspectionTaskFields
{
    public string? TaskTitle { get; init; }

    public string? TaskCategory { get; init; }

    public string? TaskDescription { get; init; }

    public string? ResponseType { get; init; }

    public string? Unit { get; init; }

    public int? EstimatedDurationMinutes { get; init; }

    public double? MinValue { get; init; }

    public double? MaxValue { get; init; }

    public string? Procedure { get; init; }

    public bool? ReadingRequired { get; init; }

    public bool? PhotoRequiredOnFailure { get; init; }

    public bool? CreateFindingOutsideLimits { get; init; }

    public string? DefaultSeverity { get; init; }

    public bool? IsCriticalTask { get; init; }

    public bool? IsMandatory { get; init; }

    public bool? IsActive { get; init; }

    public string? AssetId { get; init; }

    public string? AssetName { get; init; }

    public string? Status { get; init; }

    protected void ApplyFields(InspectionTaskEntity task)
    {
        task.TaskTitle = TaskTitle ?? task.TaskTitle;
        task.TaskCategory = TaskCategory ?? task.TaskCategory;
        task.TaskDescription = TaskDescription ?? task.TaskDescription;
        task.ResponseType = ResponseType ?? task.ResponseType;
        task.Unit = Unit ?? task.Unit;
        task.EstimatedDurationMinutes = EstimatedDurationMinutes ?? task.EstimatedDurationMinutes;
        task.MinValue = MinValue;
        task.MaxValue = MaxValue;
        task.Procedure = Procedure ?? task.Procedure;
        task.ReadingRequired = ReadingRequired ?? task.ReadingRequired;
        task.PhotoRequiredOnFailure = PhotoRequiredOnFailure ?? task.PhotoRequiredOnFailure;
        task.CreateFindingOutsideLimits = CreateFindingOutsideLimits ?? task.CreateFindingOutsideLimits;
        task.DefaultSeverity = DefaultSeverity ?? task.DefaultSeverity;
        task.IsCriticalTask = IsCriticalTask ?? task.IsCriticalTask;
        task.IsMandatory = IsMandatory ?? task.IsMandatory;
        task.IsActive = IsActive ?? task.IsActive;
        task.AssetId = AssetId ?? task.AssetId;
        task.AssetName = AssetName ?? task.AssetName;
    }
}

public sealed record CreateInspectionTaskRequest : InspectionTaskFields
{
    public string? CreatedBy { get; init; }

    public string? ClientId { get; init; }

    public string? TenantId { get; init; }

    public InspectionTaskEntity ToEntity(string taskCode)
    {
        var task = new InspectionTaskEntity
        {
            TaskCode = taskCode,
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };

        ApplyFields(task);
        return task;
    }
}

public sealed record UpdateInspectionTaskRequest : InspectionTaskFields
{
    public string? UpdatedBy { get; init; }

    public void ApplyTo(InspectionTaskEntity task)
    {
        ApplyFields(task);
        task.Status = Status ?? task.Status;
        task.UpdatedBy = UpdatedBy;
        task.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record InspectionTaskResponse : AuditResponse
{
    private InspectionTaskResponse(InspectionTaskEntity task)
        : base(task)
    {
    }

    public string TaskCode { get; init; } = string.Empty;

    public string TaskTitle { get; init; } = default!;

    public string TaskCategory { get; init; } = default!;

    public string TaskDescription { get; init; } = default!;

    public string ResponseType { get; init; } = default!;

    public string Unit { get; init; } = default!;

    public int EstimatedDurationMinutes { get; init; }

    public double? MinValue { get; init; }

    public double? MaxValue { get; init; }

    public string Procedure { get; init; } = default!;

    public bool ReadingRequired { get; init; }

    public bool PhotoRequiredOnFailure { get; init; }

    public bool CreateFindingOutsideLimits { get; init; }

    public string DefaultSeverity { get; init; } = default!;

    public bool IsCriticalTask { get; init; }

    public bool IsMandatory { get; init; }

    public bool IsActive { get; init; }

    public string AssetId { get; init; } = default!;

    public string AssetName { get; init; } = default!;

    public static InspectionTaskResponse FromEntity(InspectionTaskEntity task)
    {
        return new InspectionTaskResponse(task)
        {
            TaskCode = task.TaskCode,
            TaskTitle = task.TaskTitle,
            TaskCategory = task.TaskCategory,
            TaskDescription = task.TaskDescription,
            ResponseType = task.ResponseType,
            Unit = task.Unit,
            EstimatedDurationMinutes = task.EstimatedDurationMinutes,
            MinValue = task.MinValue,
            MaxValue = task.MaxValue,
            Procedure = task.Procedure,
            ReadingRequired = task.ReadingRequired,
            PhotoRequiredOnFailure = task.PhotoRequiredOnFailure,
            CreateFindingOutsideLimits = task.CreateFindingOutsideLimits,
            DefaultSeverity = task.DefaultSeverity,
            IsCriticalTask = task.IsCriticalTask,
            IsMandatory = task.IsMandatory,
            IsActive = task.IsActive,
            AssetId = task.AssetId,
            AssetName = task.AssetName
        };
    }
}
