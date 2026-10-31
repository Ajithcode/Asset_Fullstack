using A360.Inspection.Domain.Entities;
using InspectionTypeEntity = A360.Inspection.Domain.Entities.InspectionType;

namespace A360.Inspection.Api.Contracts;

/// <summary>Editable fields shared by the create and update requests for InspectionType.</summary>
public abstract record InspectionTypeFields
{
    public string? InspectionTypeName { get; init; }

    public string? Category { get; init; }

    public string? Description { get; init; }

    public string? DefaultChecklistCode { get; init; }

    public string? DefaultChecklistName { get; init; }

    public string? DefaultPriority { get; init; }

    public string? DefaultFrequency { get; init; }

    public string? ApplicableAssets { get; init; }

    public string? ResponsibleRole { get; init; }

    public bool? PhotoEvidenceOnFailure { get; init; }

    public bool? SignatureRequired { get; init; }

    public bool? CorrectiveActionOnFailure { get; init; }

    public bool? PermitRequired { get; init; }

    public bool? IsActive { get; init; }

    public string? AssetId { get; init; }

    public string? AssetName { get; init; }

    public string? DefaultApprovalWorkflow { get; init; }

    public string? DefaultReportTemplate { get; init; }

    public string? Status { get; init; }

    protected void ApplyFields(InspectionTypeEntity inspectionType)
    {
        inspectionType.InspectionTypeName = InspectionTypeName ?? inspectionType.InspectionTypeName;
        inspectionType.Category = Category ?? inspectionType.Category;
        inspectionType.Description = Description ?? inspectionType.Description;
        inspectionType.DefaultChecklistCode = DefaultChecklistCode ?? inspectionType.DefaultChecklistCode;
        inspectionType.DefaultChecklistName = DefaultChecklistName ?? inspectionType.DefaultChecklistName;
        inspectionType.DefaultPriority = DefaultPriority ?? inspectionType.DefaultPriority;
        inspectionType.DefaultFrequency = DefaultFrequency ?? inspectionType.DefaultFrequency;
        inspectionType.ApplicableAssets = ApplicableAssets ?? inspectionType.ApplicableAssets;
        inspectionType.ResponsibleRole = ResponsibleRole ?? inspectionType.ResponsibleRole;
        inspectionType.PhotoEvidenceOnFailure = PhotoEvidenceOnFailure ?? inspectionType.PhotoEvidenceOnFailure;
        inspectionType.SignatureRequired = SignatureRequired ?? inspectionType.SignatureRequired;
        inspectionType.CorrectiveActionOnFailure = CorrectiveActionOnFailure ?? inspectionType.CorrectiveActionOnFailure;
        inspectionType.PermitRequired = PermitRequired ?? inspectionType.PermitRequired;
        inspectionType.IsActive = IsActive ?? inspectionType.IsActive;
        inspectionType.AssetId = AssetId ?? inspectionType.AssetId;
        inspectionType.AssetName = AssetName ?? inspectionType.AssetName;
        inspectionType.DefaultApprovalWorkflow = DefaultApprovalWorkflow ?? inspectionType.DefaultApprovalWorkflow;
        inspectionType.DefaultReportTemplate = DefaultReportTemplate ?? inspectionType.DefaultReportTemplate;
    }
}

public sealed record CreateInspectionTypeRequest : InspectionTypeFields
{
    public string? CreatedBy { get; init; }

    public string? ClientId { get; init; }

    public string? TenantId { get; init; }

    public InspectionTypeEntity ToEntity(string inspectionTypeCode)
    {
        var inspectionType = new InspectionTypeEntity
        {
            InspectionTypeCode = inspectionTypeCode,
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };

        ApplyFields(inspectionType);
        return inspectionType;
    }
}

public sealed record UpdateInspectionTypeRequest : InspectionTypeFields
{
    public string? UpdatedBy { get; init; }

    public void ApplyTo(InspectionTypeEntity inspectionType)
    {
        ApplyFields(inspectionType);
        inspectionType.Status = Status ?? inspectionType.Status;
        inspectionType.UpdatedBy = UpdatedBy;
        inspectionType.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record InspectionTypeResponse : AuditResponse
{
    private InspectionTypeResponse(InspectionTypeEntity inspectionType)
        : base(inspectionType)
    {
    }

    public string InspectionTypeCode { get; init; } = string.Empty;

    public string InspectionTypeName { get; init; } = default!;

    public string Category { get; init; } = default!;

    public string Description { get; init; } = default!;

    public string DefaultChecklistCode { get; init; } = default!;

    public string DefaultChecklistName { get; init; } = default!;

    public string DefaultPriority { get; init; } = default!;

    public string DefaultFrequency { get; init; } = default!;

    public string ApplicableAssets { get; init; } = default!;

    public string ResponsibleRole { get; init; } = default!;

    public bool PhotoEvidenceOnFailure { get; init; }

    public bool SignatureRequired { get; init; }

    public bool CorrectiveActionOnFailure { get; init; }

    public bool PermitRequired { get; init; }

    public bool IsActive { get; init; }

    public string AssetId { get; init; } = default!;

    public string AssetName { get; init; } = default!;

    public string DefaultApprovalWorkflow { get; init; } = default!;

    public string DefaultReportTemplate { get; init; } = default!;

    public static InspectionTypeResponse FromEntity(InspectionTypeEntity inspectionType)
    {
        return new InspectionTypeResponse(inspectionType)
        {
            InspectionTypeCode = inspectionType.InspectionTypeCode,
            InspectionTypeName = inspectionType.InspectionTypeName,
            Category = inspectionType.Category,
            Description = inspectionType.Description,
            DefaultChecklistCode = inspectionType.DefaultChecklistCode,
            DefaultChecklistName = inspectionType.DefaultChecklistName,
            DefaultPriority = inspectionType.DefaultPriority,
            DefaultFrequency = inspectionType.DefaultFrequency,
            ApplicableAssets = inspectionType.ApplicableAssets,
            ResponsibleRole = inspectionType.ResponsibleRole,
            PhotoEvidenceOnFailure = inspectionType.PhotoEvidenceOnFailure,
            SignatureRequired = inspectionType.SignatureRequired,
            CorrectiveActionOnFailure = inspectionType.CorrectiveActionOnFailure,
            PermitRequired = inspectionType.PermitRequired,
            IsActive = inspectionType.IsActive,
            AssetId = inspectionType.AssetId,
            AssetName = inspectionType.AssetName,
            DefaultApprovalWorkflow = inspectionType.DefaultApprovalWorkflow,
            DefaultReportTemplate = inspectionType.DefaultReportTemplate
        };
    }
}
