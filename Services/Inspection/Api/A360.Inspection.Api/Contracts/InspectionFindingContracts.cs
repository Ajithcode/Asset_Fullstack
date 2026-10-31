using A360.Inspection.Domain.Entities;
using InspectionFindingEntity = A360.Inspection.Domain.Entities.InspectionFinding;

namespace A360.Inspection.Api.Contracts;

/// <summary>Editable fields shared by the create and update requests for InspectionFinding.</summary>
public abstract record InspectionFindingFields
{
    public string? Title { get; init; }

    public string? InspectionCode { get; init; }

    public string? AssetCode { get; init; }

    public string? AssetName { get; init; }

    public string? SourceTask { get; init; }

    public string? Severity { get; init; }

    public string? Owner { get; init; }

    public string? DueDate { get; init; }

    public string? RecordedValue { get; init; }

    public string? Acceptance { get; init; }

    public string? Observation { get; init; }

    public List<string>? Evidence { get; init; }

    public string? CorrectiveAction { get; init; }

    public string? AssignedTo { get; init; }

    public string? WorkOrder { get; init; }

    public string? WorkOrderStatus { get; init; }

    public string? WorkflowStep { get; init; }

    public List<FindingActivity>? Activity { get; init; }

    public string? Status { get; init; }

    protected void ApplyFields(InspectionFindingEntity finding)
    {
        finding.Title = Title ?? finding.Title;
        finding.InspectionCode = InspectionCode ?? finding.InspectionCode;
        finding.AssetCode = AssetCode ?? finding.AssetCode;
        finding.AssetName = AssetName ?? finding.AssetName;
        finding.SourceTask = SourceTask ?? finding.SourceTask;
        finding.Severity = Severity ?? finding.Severity;
        finding.Owner = Owner ?? finding.Owner;
        finding.DueDate = DueDate;
        finding.RecordedValue = RecordedValue ?? finding.RecordedValue;
        finding.Acceptance = Acceptance ?? finding.Acceptance;
        finding.Observation = Observation ?? finding.Observation;
        finding.Evidence = Evidence ?? finding.Evidence;
        finding.CorrectiveAction = CorrectiveAction ?? finding.CorrectiveAction;
        finding.AssignedTo = AssignedTo ?? finding.AssignedTo;
        finding.WorkOrder = WorkOrder ?? finding.WorkOrder;
        finding.WorkOrderStatus = WorkOrderStatus ?? finding.WorkOrderStatus;
        finding.WorkflowStep = WorkflowStep ?? finding.WorkflowStep;
        finding.Activity = Activity ?? finding.Activity;
    }
}

public sealed record CreateInspectionFindingRequest : InspectionFindingFields
{
    public string? CreatedBy { get; init; }

    public string? ClientId { get; init; }

    public string? TenantId { get; init; }

    public InspectionFindingEntity ToEntity(string findingCode)
    {
        var finding = new InspectionFindingEntity
        {
            FindingCode = findingCode,
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };

        ApplyFields(finding);
        return finding;
    }
}

public sealed record UpdateInspectionFindingRequest : InspectionFindingFields
{
    public string? UpdatedBy { get; init; }

    public void ApplyTo(InspectionFindingEntity finding)
    {
        ApplyFields(finding);
        finding.Status = Status ?? finding.Status;
        finding.UpdatedBy = UpdatedBy;
        finding.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record InspectionFindingResponse : AuditResponse
{
    private InspectionFindingResponse(InspectionFindingEntity finding)
        : base(finding)
    {
    }

    public string FindingCode { get; init; } = string.Empty;

    public string Title { get; init; } = default!;

    public string InspectionCode { get; init; } = default!;

    public string AssetCode { get; init; } = default!;

    public string AssetName { get; init; } = default!;

    public string SourceTask { get; init; } = default!;

    public string Severity { get; init; } = default!;

    public string Owner { get; init; } = default!;

    public string? DueDate { get; init; }

    public string RecordedValue { get; init; } = default!;

    public string Acceptance { get; init; } = default!;

    public string Observation { get; init; } = default!;

    public List<string> Evidence { get; init; } = default!;

    public string CorrectiveAction { get; init; } = default!;

    public string AssignedTo { get; init; } = default!;

    public string WorkOrder { get; init; } = default!;

    public string WorkOrderStatus { get; init; } = default!;

    public string WorkflowStep { get; init; } = default!;

    public List<FindingActivity> Activity { get; init; } = default!;

    public static InspectionFindingResponse FromEntity(InspectionFindingEntity finding)
    {
        return new InspectionFindingResponse(finding)
        {
            FindingCode = finding.FindingCode,
            Title = finding.Title,
            InspectionCode = finding.InspectionCode,
            AssetCode = finding.AssetCode,
            AssetName = finding.AssetName,
            SourceTask = finding.SourceTask,
            Severity = finding.Severity,
            Owner = finding.Owner,
            DueDate = finding.DueDate,
            RecordedValue = finding.RecordedValue,
            Acceptance = finding.Acceptance,
            Observation = finding.Observation,
            Evidence = finding.Evidence,
            CorrectiveAction = finding.CorrectiveAction,
            AssignedTo = finding.AssignedTo,
            WorkOrder = finding.WorkOrder,
            WorkOrderStatus = finding.WorkOrderStatus,
            WorkflowStep = finding.WorkflowStep,
            Activity = finding.Activity
        };
    }
}
