using A360.Inspection.Domain.Entities;
using InspectionEntity = A360.Inspection.Domain.Entities.Inspection;

namespace A360.Inspection.Api.Contracts;

/// <summary>Editable fields shared by the create and update requests for Inspection.</summary>
public abstract record InspectionFields
{
    public string? Title { get; init; }

    public string? InspectionTypeName { get; init; }

    public string? TypeShort { get; init; }

    public string? Priority { get; init; }

    public string? SiteName { get; init; }

    public string? Department { get; init; }

    public string? PlanCode { get; init; }

    public string? PlanName { get; init; }

    public string? Trigger { get; init; }

    public string? Description { get; init; }

    public string? Scope { get; init; }

    public string? Category { get; init; }

    public string? ReferenceStandard { get; init; }

    public bool? PermitRequired { get; init; }

    public bool? EvidenceRequired { get; init; }

    public string? AssetCode { get; init; }

    public string? AssetName { get; init; }

    public string? Location { get; init; }

    public string? AssignedTo { get; init; }

    public string? AssignedTeam { get; init; }

    public string? DueDate { get; init; }

    public string? ScheduledStart { get; init; }

    public string? ScheduledEnd { get; init; }

    public string? StartedAt { get; init; }

    public string? CompletedAt { get; init; }

    public int? FindingsCount { get; init; }

    public string? ChecklistCode { get; init; }

    public string? ChecklistName { get; init; }

    public List<InspectionChecklistResponse>? Responses { get; init; }

    public List<SignOffStep>? SignOff { get; init; }

    public string? Status { get; init; }

    protected void ApplyFields(InspectionEntity inspection)
    {
        inspection.Title = Title ?? inspection.Title;
        inspection.InspectionTypeName = InspectionTypeName ?? inspection.InspectionTypeName;
        inspection.TypeShort = TypeShort ?? inspection.TypeShort;
        inspection.Priority = Priority ?? inspection.Priority;
        inspection.SiteName = SiteName ?? inspection.SiteName;
        inspection.Department = Department ?? inspection.Department;
        inspection.PlanCode = PlanCode ?? inspection.PlanCode;
        inspection.PlanName = PlanName ?? inspection.PlanName;
        inspection.Trigger = Trigger ?? inspection.Trigger;
        inspection.Description = Description ?? inspection.Description;
        inspection.Scope = Scope ?? inspection.Scope;
        inspection.Category = Category ?? inspection.Category;
        inspection.ReferenceStandard = ReferenceStandard ?? inspection.ReferenceStandard;
        inspection.PermitRequired = PermitRequired ?? inspection.PermitRequired;
        inspection.EvidenceRequired = EvidenceRequired ?? inspection.EvidenceRequired;
        inspection.AssetCode = AssetCode ?? inspection.AssetCode;
        inspection.AssetName = AssetName ?? inspection.AssetName;
        inspection.Location = Location ?? inspection.Location;
        inspection.AssignedTo = AssignedTo ?? inspection.AssignedTo;
        inspection.AssignedTeam = AssignedTeam ?? inspection.AssignedTeam;
        inspection.DueDate = DueDate;
        inspection.ScheduledStart = ScheduledStart;
        inspection.ScheduledEnd = ScheduledEnd;
        inspection.StartedAt = StartedAt;
        inspection.CompletedAt = CompletedAt;
        inspection.FindingsCount = FindingsCount ?? inspection.FindingsCount;
        inspection.ChecklistCode = ChecklistCode ?? inspection.ChecklistCode;
        inspection.ChecklistName = ChecklistName ?? inspection.ChecklistName;
        inspection.Responses = Responses ?? inspection.Responses;
        inspection.SignOff = SignOff ?? inspection.SignOff;
    }
}

public sealed record CreateInspectionRequest : InspectionFields
{
    public string? CreatedBy { get; init; }

    public string? ClientId { get; init; }

    public string? TenantId { get; init; }

    public InspectionEntity ToEntity(string inspectionCode)
    {
        var inspection = new InspectionEntity
        {
            InspectionCode = inspectionCode,
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };

        ApplyFields(inspection);
        return inspection;
    }
}

public sealed record UpdateInspectionRequest : InspectionFields
{
    public string? UpdatedBy { get; init; }

    public void ApplyTo(InspectionEntity inspection)
    {
        ApplyFields(inspection);
        inspection.Status = Status ?? inspection.Status;
        inspection.UpdatedBy = UpdatedBy;
        inspection.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record InspectionResponse : AuditResponse
{
    private InspectionResponse(InspectionEntity inspection)
        : base(inspection)
    {
    }

    public string InspectionCode { get; init; } = string.Empty;

    public string Title { get; init; } = default!;

    public string InspectionTypeName { get; init; } = default!;

    public string TypeShort { get; init; } = default!;

    public string Priority { get; init; } = default!;

    public string SiteName { get; init; } = default!;

    public string Department { get; init; } = default!;

    public string PlanCode { get; init; } = default!;

    public string PlanName { get; init; } = default!;

    public string Trigger { get; init; } = default!;

    public string Description { get; init; } = default!;

    public string Scope { get; init; } = default!;

    public string Category { get; init; } = default!;

    public string ReferenceStandard { get; init; } = default!;

    public bool PermitRequired { get; init; }

    public bool EvidenceRequired { get; init; }

    public string AssetCode { get; init; } = default!;

    public string AssetName { get; init; } = default!;

    public string Location { get; init; } = default!;

    public string AssignedTo { get; init; } = default!;

    public string AssignedTeam { get; init; } = default!;

    public string? DueDate { get; init; }

    public string? ScheduledStart { get; init; }

    public string? ScheduledEnd { get; init; }

    public string? StartedAt { get; init; }

    public string? CompletedAt { get; init; }

    public int FindingsCount { get; init; }

    public string ChecklistCode { get; init; } = default!;

    public string ChecklistName { get; init; } = default!;

    public List<InspectionChecklistResponse> Responses { get; init; } = default!;

    public List<SignOffStep> SignOff { get; init; } = default!;

    public static InspectionResponse FromEntity(InspectionEntity inspection)
    {
        return new InspectionResponse(inspection)
        {
            InspectionCode = inspection.InspectionCode,
            Title = inspection.Title,
            InspectionTypeName = inspection.InspectionTypeName,
            TypeShort = inspection.TypeShort,
            Priority = inspection.Priority,
            SiteName = inspection.SiteName,
            Department = inspection.Department,
            PlanCode = inspection.PlanCode,
            PlanName = inspection.PlanName,
            Trigger = inspection.Trigger,
            Description = inspection.Description,
            Scope = inspection.Scope,
            Category = inspection.Category,
            ReferenceStandard = inspection.ReferenceStandard,
            PermitRequired = inspection.PermitRequired,
            EvidenceRequired = inspection.EvidenceRequired,
            AssetCode = inspection.AssetCode,
            AssetName = inspection.AssetName,
            Location = inspection.Location,
            AssignedTo = inspection.AssignedTo,
            AssignedTeam = inspection.AssignedTeam,
            DueDate = inspection.DueDate,
            ScheduledStart = inspection.ScheduledStart,
            ScheduledEnd = inspection.ScheduledEnd,
            StartedAt = inspection.StartedAt,
            CompletedAt = inspection.CompletedAt,
            FindingsCount = inspection.FindingsCount,
            ChecklistCode = inspection.ChecklistCode,
            ChecklistName = inspection.ChecklistName,
            Responses = inspection.Responses,
            SignOff = inspection.SignOff
        };
    }
}
