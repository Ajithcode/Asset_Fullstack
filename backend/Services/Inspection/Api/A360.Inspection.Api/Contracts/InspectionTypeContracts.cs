using InspectionTypeEntity = A360.Inspection.Domain.Entities.InspectionType;

namespace A360.Inspection.Api.Contracts;

public sealed record CreateInspectionTypeRequest(
    string? AssetId,
    string? AssetName,
    string? InspectionTypeName,
    string? Description,
    string? DefaultPriority,
    string? DefaultApprovalWorkflow,
    string? DefaultReportTemplate,
    bool? IsActive,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public InspectionTypeEntity ToEntity(string inspectionTypeCode)
    {
        return new InspectionTypeEntity
        {
            InspectionTypeCode = inspectionTypeCode,
            AssetId = AssetId ?? string.Empty,
            AssetName = AssetName ?? string.Empty,
            InspectionTypeName = InspectionTypeName ?? string.Empty,
            Description = Description ?? string.Empty,
            DefaultPriority = DefaultPriority ?? string.Empty,
            DefaultApprovalWorkflow = DefaultApprovalWorkflow ?? string.Empty,
            DefaultReportTemplate = DefaultReportTemplate ?? string.Empty,
            IsActive = IsActive ?? true,
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };
    }
}

public sealed record UpdateInspectionTypeRequest(
    string? AssetId,
    string? AssetName,
    string? InspectionTypeName,
    string? Description,
    string? DefaultPriority,
    string? DefaultApprovalWorkflow,
    string? DefaultReportTemplate,
    bool? IsActive,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(InspectionTypeEntity inspectionType)
    {
        inspectionType.AssetId = AssetId ?? string.Empty;
        inspectionType.AssetName = AssetName ?? string.Empty;
        inspectionType.InspectionTypeName = InspectionTypeName ?? string.Empty;
        inspectionType.Description = Description ?? string.Empty;
        inspectionType.DefaultPriority = DefaultPriority ?? string.Empty;
        inspectionType.DefaultApprovalWorkflow = DefaultApprovalWorkflow ?? string.Empty;
        inspectionType.DefaultReportTemplate = DefaultReportTemplate ?? string.Empty;
        inspectionType.IsActive = IsActive ?? inspectionType.IsActive;
        inspectionType.Status = Status;
        inspectionType.UpdatedBy = UpdatedBy;
        inspectionType.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record InspectionTypeResponse(
    string Id,
    string InspectionTypeCode,
    string AssetId,
    string AssetName,
    string InspectionTypeName,
    string Description,
    string DefaultPriority,
    string DefaultApprovalWorkflow,
    string DefaultReportTemplate,
    bool IsActive,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    bool IsDeleted)
{
    public static InspectionTypeResponse FromEntity(InspectionTypeEntity inspectionType)
    {
        return new InspectionTypeResponse(
            inspectionType.Id,
            inspectionType.InspectionTypeCode,
            inspectionType.AssetId,
            inspectionType.AssetName,
            inspectionType.InspectionTypeName,
            inspectionType.Description,
            inspectionType.DefaultPriority,
            inspectionType.DefaultApprovalWorkflow,
            inspectionType.DefaultReportTemplate,
            inspectionType.IsActive,
            inspectionType.Status,
            inspectionType.CreatedBy,
            inspectionType.CreatedAt,
            inspectionType.UpdatedBy,
            inspectionType.UpdatedAt,
            inspectionType.ClientId,
            inspectionType.TenantId,
            inspectionType.IsDeleted);
    }
}
