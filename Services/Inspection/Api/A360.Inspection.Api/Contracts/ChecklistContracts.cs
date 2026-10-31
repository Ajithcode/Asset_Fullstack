using A360.Inspection.Domain.Entities;
using ChecklistEntity = A360.Inspection.Domain.Entities.Checklist;

namespace A360.Inspection.Api.Contracts;

/// <summary>Editable fields shared by the create and update requests for Checklist.</summary>
public abstract record ChecklistFields
{
    public string? TemplateName { get; init; }

    public string? InspectionTypeId { get; init; }

    public string? InspectionTypeName { get; init; }

    public string? AssetCategory { get; init; }

    public string? AssetType { get; init; }

    public List<string>? Modules { get; init; }

    public string? Scoring { get; init; }

    public string? Owner { get; init; }

    public int? Version { get; init; }

    public string? VersionLabel { get; init; }

    public string? EffectiveDate { get; init; }

    public List<ChecklistSection>? Sections { get; init; }

    public bool? AllMandatoryRequired { get; init; }

    public bool? PhotoOnFailedItems { get; init; }

    public bool? InspectorSignatureRequired { get; init; }

    public bool? CriticalFailureOverridesScore { get; init; }

    public bool? CreateFindingOnFailure { get; init; }

    public bool? RouteCorrectiveAction { get; init; }

    public bool? IsActive { get; init; }

    public string? Status { get; init; }

    protected void ApplyFields(ChecklistEntity checklist)
    {
        checklist.TemplateName = TemplateName ?? checklist.TemplateName;
        checklist.InspectionTypeId = InspectionTypeId ?? checklist.InspectionTypeId;
        checklist.InspectionTypeName = InspectionTypeName ?? checklist.InspectionTypeName;
        checklist.AssetCategory = AssetCategory ?? checklist.AssetCategory;
        checklist.AssetType = AssetType ?? checklist.AssetType;
        checklist.Modules = Modules ?? checklist.Modules;
        checklist.Scoring = Scoring ?? checklist.Scoring;
        checklist.Owner = Owner ?? checklist.Owner;
        checklist.Version = Version ?? checklist.Version;
        checklist.VersionLabel = VersionLabel ?? checklist.VersionLabel;
        checklist.EffectiveDate = ContractDates.ParseDate(EffectiveDate);
        checklist.Sections = Sections ?? checklist.Sections;
        checklist.AllMandatoryRequired = AllMandatoryRequired ?? checklist.AllMandatoryRequired;
        checklist.PhotoOnFailedItems = PhotoOnFailedItems ?? checklist.PhotoOnFailedItems;
        checklist.InspectorSignatureRequired = InspectorSignatureRequired ?? checklist.InspectorSignatureRequired;
        checklist.CriticalFailureOverridesScore = CriticalFailureOverridesScore ?? checklist.CriticalFailureOverridesScore;
        checklist.CreateFindingOnFailure = CreateFindingOnFailure ?? checklist.CreateFindingOnFailure;
        checklist.RouteCorrectiveAction = RouteCorrectiveAction ?? checklist.RouteCorrectiveAction;
        checklist.IsActive = IsActive ?? checklist.IsActive;
    }
}

public sealed record CreateChecklistRequest : ChecklistFields
{
    public string? CreatedBy { get; init; }

    public string? ClientId { get; init; }

    public string? TenantId { get; init; }

    public ChecklistEntity ToEntity(string checklistCode)
    {
        var checklist = new ChecklistEntity
        {
            ChecklistCode = checklistCode,
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };

        ApplyFields(checklist);
        return checklist;
    }
}

public sealed record UpdateChecklistRequest : ChecklistFields
{
    public string? UpdatedBy { get; init; }

    public void ApplyTo(ChecklistEntity checklist)
    {
        ApplyFields(checklist);
        checklist.Status = Status ?? checklist.Status;
        checklist.UpdatedBy = UpdatedBy;
        checklist.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record ChecklistResponse : AuditResponse
{
    private ChecklistResponse(ChecklistEntity checklist)
        : base(checklist)
    {
    }

    public string ChecklistCode { get; init; } = string.Empty;

    public string TemplateName { get; init; } = default!;

    public string InspectionTypeId { get; init; } = default!;

    public string InspectionTypeName { get; init; } = default!;

    public string AssetCategory { get; init; } = default!;

    public string AssetType { get; init; } = default!;

    public List<string> Modules { get; init; } = default!;

    public string Scoring { get; init; } = default!;

    public string Owner { get; init; } = default!;

    public int Version { get; init; }

    public string VersionLabel { get; init; } = default!;

    public string? EffectiveDate { get; init; }

    public List<ChecklistSection> Sections { get; init; } = default!;

    public bool AllMandatoryRequired { get; init; }

    public bool PhotoOnFailedItems { get; init; }

    public bool InspectorSignatureRequired { get; init; }

    public bool CriticalFailureOverridesScore { get; init; }

    public bool CreateFindingOnFailure { get; init; }

    public bool RouteCorrectiveAction { get; init; }

    public bool IsActive { get; init; }

    public static ChecklistResponse FromEntity(ChecklistEntity checklist)
    {
        return new ChecklistResponse(checklist)
        {
            ChecklistCode = checklist.ChecklistCode,
            TemplateName = checklist.TemplateName,
            InspectionTypeId = checklist.InspectionTypeId,
            InspectionTypeName = checklist.InspectionTypeName,
            AssetCategory = checklist.AssetCategory,
            AssetType = checklist.AssetType,
            Modules = checklist.Modules,
            Scoring = checklist.Scoring,
            Owner = checklist.Owner,
            Version = checklist.Version,
            VersionLabel = checklist.VersionLabel,
            EffectiveDate = ContractDates.ToDateString(checklist.EffectiveDate),
            Sections = checklist.Sections,
            AllMandatoryRequired = checklist.AllMandatoryRequired,
            PhotoOnFailedItems = checklist.PhotoOnFailedItems,
            InspectorSignatureRequired = checklist.InspectorSignatureRequired,
            CriticalFailureOverridesScore = checklist.CriticalFailureOverridesScore,
            CreateFindingOnFailure = checklist.CreateFindingOnFailure,
            RouteCorrectiveAction = checklist.RouteCorrectiveAction,
            IsActive = checklist.IsActive
        };
    }
}
