using A360.Inspection.Domain.Entities;
using InspectionPlanEntity = A360.Inspection.Domain.Entities.InspectionPlan;

namespace A360.Inspection.Api.Contracts;

/// <summary>Editable fields shared by the create and update requests for InspectionPlan.</summary>
public abstract record InspectionPlanFields
{
    public string? PlanName { get; init; }

    public string? InspectionTypeName { get; init; }

    public string? Category { get; init; }

    public string? Frequency { get; init; }

    public int? AssetCount { get; init; }

    public string? NextDueDate { get; init; }

    public string? InspectorName { get; init; }

    public string? ChecklistCode { get; init; }

    public string? ChecklistName { get; init; }

    public int? LeadTimeDays { get; init; }

    public bool? AutoGenerate { get; init; }

    public bool? IsActive { get; init; }

    public string? Status { get; init; }

    protected void ApplyFields(InspectionPlanEntity plan)
    {
        plan.PlanName = PlanName ?? plan.PlanName;
        plan.InspectionTypeName = InspectionTypeName ?? plan.InspectionTypeName;
        plan.Category = Category ?? plan.Category;
        plan.Frequency = Frequency ?? plan.Frequency;
        plan.AssetCount = AssetCount ?? plan.AssetCount;
        plan.NextDueDate = NextDueDate;
        plan.InspectorName = InspectorName ?? plan.InspectorName;
        plan.ChecklistCode = ChecklistCode ?? plan.ChecklistCode;
        plan.ChecklistName = ChecklistName ?? plan.ChecklistName;
        plan.LeadTimeDays = LeadTimeDays ?? plan.LeadTimeDays;
        plan.AutoGenerate = AutoGenerate ?? plan.AutoGenerate;
        plan.IsActive = IsActive ?? plan.IsActive;
    }
}

public sealed record CreateInspectionPlanRequest : InspectionPlanFields
{
    public string? CreatedBy { get; init; }

    public string? ClientId { get; init; }

    public string? TenantId { get; init; }

    public InspectionPlanEntity ToEntity(string planCode)
    {
        var plan = new InspectionPlanEntity
        {
            PlanCode = planCode,
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };

        ApplyFields(plan);
        return plan;
    }
}

public sealed record UpdateInspectionPlanRequest : InspectionPlanFields
{
    public string? UpdatedBy { get; init; }

    public void ApplyTo(InspectionPlanEntity plan)
    {
        ApplyFields(plan);
        plan.Status = Status ?? plan.Status;
        plan.UpdatedBy = UpdatedBy;
        plan.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record InspectionPlanResponse : AuditResponse
{
    private InspectionPlanResponse(InspectionPlanEntity plan)
        : base(plan)
    {
    }

    public string PlanCode { get; init; } = string.Empty;

    public string PlanName { get; init; } = default!;

    public string InspectionTypeName { get; init; } = default!;

    public string Category { get; init; } = default!;

    public string Frequency { get; init; } = default!;

    public int AssetCount { get; init; }

    public string? NextDueDate { get; init; }

    public string InspectorName { get; init; } = default!;

    public string ChecklistCode { get; init; } = default!;

    public string ChecklistName { get; init; } = default!;

    public int LeadTimeDays { get; init; }

    public bool AutoGenerate { get; init; }

    public bool IsActive { get; init; }

    public static InspectionPlanResponse FromEntity(InspectionPlanEntity plan)
    {
        return new InspectionPlanResponse(plan)
        {
            PlanCode = plan.PlanCode,
            PlanName = plan.PlanName,
            InspectionTypeName = plan.InspectionTypeName,
            Category = plan.Category,
            Frequency = plan.Frequency,
            AssetCount = plan.AssetCount,
            NextDueDate = plan.NextDueDate,
            InspectorName = plan.InspectorName,
            ChecklistCode = plan.ChecklistCode,
            ChecklistName = plan.ChecklistName,
            LeadTimeDays = plan.LeadTimeDays,
            AutoGenerate = plan.AutoGenerate,
            IsActive = plan.IsActive
        };
    }
}
