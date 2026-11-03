
using ComplianceInspectionEntity = A360.Maintenance.Domain.Entities.ComplianceInspection;

namespace A360.Maintenance.Api.Contracts;

public sealed record CreateComplianceInspectionRequest(
    string? InspectionId,
    string? AssetId,
    string? AssetName,
    string? InspectionType,
    List<string>? Checklist,
    string? InspectorName,
    string? Result,
    DateTime? NextInspectionDate,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public ComplianceInspectionEntity ToEntity()
    {
        return new ComplianceInspectionEntity
        {
            InspectionId = InspectionId,
            AssetId = AssetId,
            AssetName = AssetName,
            InspectionType = InspectionType,
            Checklist = Checklist ?? new List<string>(),
            InspectorName = InspectorName,
            Result = Result,
            NextInspectionDate = NextInspectionDate,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateComplianceInspectionRequest(
    string? InspectionId,
    string? AssetId,
    string? AssetName,
    string? InspectionType,
    List<string>? Checklist,
    string? InspectorName,
    string? Result,
    DateTime? NextInspectionDate,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(ComplianceInspectionEntity complianceInspection)
    {
        complianceInspection.InspectionId = InspectionId;
        complianceInspection.AssetId = AssetId;
        complianceInspection.AssetName = AssetName;
        complianceInspection.InspectionType = InspectionType;
        complianceInspection.Checklist = Checklist ?? new List<string>();
        complianceInspection.InspectorName = InspectorName;
        complianceInspection.Result = Result;
        complianceInspection.NextInspectionDate = NextInspectionDate;

        complianceInspection.UpdatedBy = UpdatedBy;
        complianceInspection.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            complianceInspection.Status = Status;
        }
    }
}

public sealed record ComplianceInspectionResponse(
    string Id,
    string InspectionId,
    string AssetId,
    string AssetName,
    string InspectionType,
    List<string>? Checklist,
    string? InspectorName,
    string? Result,
    DateTime? NextInspectionDate,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static ComplianceInspectionResponse FromEntity(ComplianceInspectionEntity complianceInspection)
    {
        return new ComplianceInspectionResponse(
            complianceInspection.Id ?? string.Empty,
            complianceInspection.InspectionId ?? string.Empty,
            complianceInspection.AssetId ?? string.Empty,
            complianceInspection.AssetName ?? string.Empty,
            complianceInspection.InspectionType ?? string.Empty,
            complianceInspection.Checklist,
            complianceInspection.InspectorName,
            complianceInspection.Result,
            complianceInspection.NextInspectionDate,
            complianceInspection.CreatedBy,
            complianceInspection.CreatedAt,
            complianceInspection.UpdatedBy,
            complianceInspection.UpdatedAt,
            complianceInspection.ClientId,
            complianceInspection.TenantId,
            complianceInspection.Status,
            complianceInspection.IsDeleted
        );
    }
}
