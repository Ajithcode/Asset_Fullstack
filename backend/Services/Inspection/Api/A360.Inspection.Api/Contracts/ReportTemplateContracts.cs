using ReportTemplateEntity = A360.Inspection.Domain.Entities.ReportTemplate;

namespace A360.Inspection.Api.Contracts;

public sealed record CreateReportTemplateRequest(
    string? ReportTitle,
    string? ReportNumberFormat,
    bool? IncludeOrganizationLogo,
    bool? IncludeOrganizationDetails,
    bool? IncludeSiteDetails,
    bool? IncludeWorkOrderDetails,
    bool? IncludeAssetDetails,
    bool? IncludeExecutiveSummary,
    bool? IncludeInspectorDetails,
    bool? IncludeAssetSummary,
    bool? IncludeTaskResponses,
    bool? IncludePassOrFailResults,
    bool? IncludeNotes,
    bool? IncludeRemarks,
    bool? IncludePhotos,
    bool? IncludeVideoLinks,
    bool? IncludeDefects,
    bool? IncludeCorrectiveActions,
    bool? IncludeApprovalHistory,
    bool? IncludeSignatures,
    bool? IncludeStamps,
    bool? IncludeAuditDetails,
    string? Orientation,
    string? PageSize,
    string? Header,
    string? Footer,
    bool? ShowPageNumber,
    string? Watermark,
    string? ConfidentialityLabel,
    string? PhotoSize,
    int? NumberOfPhotosPerPage,
    bool? IncludeFailedTasksOnly,
    bool? IncludeCompleteInspection,
    bool? IncludePreviousInspectionComparison,
    bool? IsActive,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public ReportTemplateEntity ToEntity(string reportTemplateCode)
    {
        return new ReportTemplateEntity
        {
            ReportTemplateCode = reportTemplateCode,
            ReportTitle = ReportTitle ?? string.Empty,
            ReportNumberFormat = ReportNumberFormat ?? string.Empty,
            IncludeOrganizationLogo = IncludeOrganizationLogo ?? false,
            IncludeOrganizationDetails = IncludeOrganizationDetails ?? false,
            IncludeSiteDetails = IncludeSiteDetails ?? false,
            IncludeWorkOrderDetails = IncludeWorkOrderDetails ?? false,
            IncludeAssetDetails = IncludeAssetDetails ?? false,
            IncludeExecutiveSummary = IncludeExecutiveSummary ?? false,
            IncludeInspectorDetails = IncludeInspectorDetails ?? false,
            IncludeAssetSummary = IncludeAssetSummary ?? false,
            IncludeTaskResponses = IncludeTaskResponses ?? false,
            IncludePassOrFailResults = IncludePassOrFailResults ?? false,
            IncludeNotes = IncludeNotes ?? false,
            IncludeRemarks = IncludeRemarks ?? false,
            IncludePhotos = IncludePhotos ?? false,
            IncludeVideoLinks = IncludeVideoLinks ?? false,
            IncludeDefects = IncludeDefects ?? false,
            IncludeCorrectiveActions = IncludeCorrectiveActions ?? false,
            IncludeApprovalHistory = IncludeApprovalHistory ?? false,
            IncludeSignatures = IncludeSignatures ?? false,
            IncludeStamps = IncludeStamps ?? false,
            IncludeAuditDetails = IncludeAuditDetails ?? false,
            Orientation = Orientation ?? string.Empty,
            PageSize = PageSize ?? string.Empty,
            Header = Header ?? string.Empty,
            Footer = Footer ?? string.Empty,
            ShowPageNumber = ShowPageNumber ?? false,
            Watermark = Watermark ?? string.Empty,
            ConfidentialityLabel = ConfidentialityLabel ?? string.Empty,
            PhotoSize = PhotoSize ?? string.Empty,
            NumberOfPhotosPerPage = NumberOfPhotosPerPage ?? 0,
            IncludeFailedTasksOnly = IncludeFailedTasksOnly ?? false,
            IncludeCompleteInspection = IncludeCompleteInspection ?? false,
            IncludePreviousInspectionComparison = IncludePreviousInspectionComparison ?? false,
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

public sealed record UpdateReportTemplateRequest(
    string? ReportTitle,
    string? ReportNumberFormat,
    bool? IncludeOrganizationLogo,
    bool? IncludeOrganizationDetails,
    bool? IncludeSiteDetails,
    bool? IncludeWorkOrderDetails,
    bool? IncludeAssetDetails,
    bool? IncludeExecutiveSummary,
    bool? IncludeInspectorDetails,
    bool? IncludeAssetSummary,
    bool? IncludeTaskResponses,
    bool? IncludePassOrFailResults,
    bool? IncludeNotes,
    bool? IncludeRemarks,
    bool? IncludePhotos,
    bool? IncludeVideoLinks,
    bool? IncludeDefects,
    bool? IncludeCorrectiveActions,
    bool? IncludeApprovalHistory,
    bool? IncludeSignatures,
    bool? IncludeStamps,
    bool? IncludeAuditDetails,
    string? Orientation,
    string? PageSize,
    string? Header,
    string? Footer,
    bool? ShowPageNumber,
    string? Watermark,
    string? ConfidentialityLabel,
    string? PhotoSize,
    int? NumberOfPhotosPerPage,
    bool? IncludeFailedTasksOnly,
    bool? IncludeCompleteInspection,
    bool? IncludePreviousInspectionComparison,
    bool? IsActive,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(ReportTemplateEntity template)
    {
        template.ReportTitle = ReportTitle ?? string.Empty;
        template.ReportNumberFormat = ReportNumberFormat ?? string.Empty;
        template.IncludeOrganizationLogo = IncludeOrganizationLogo ?? template.IncludeOrganizationLogo;
        template.IncludeOrganizationDetails = IncludeOrganizationDetails ?? template.IncludeOrganizationDetails;
        template.IncludeSiteDetails = IncludeSiteDetails ?? template.IncludeSiteDetails;
        template.IncludeWorkOrderDetails = IncludeWorkOrderDetails ?? template.IncludeWorkOrderDetails;
        template.IncludeAssetDetails = IncludeAssetDetails ?? template.IncludeAssetDetails;
        template.IncludeExecutiveSummary = IncludeExecutiveSummary ?? template.IncludeExecutiveSummary;
        template.IncludeInspectorDetails = IncludeInspectorDetails ?? template.IncludeInspectorDetails;
        template.IncludeAssetSummary = IncludeAssetSummary ?? template.IncludeAssetSummary;
        template.IncludeTaskResponses = IncludeTaskResponses ?? template.IncludeTaskResponses;
        template.IncludePassOrFailResults = IncludePassOrFailResults ?? template.IncludePassOrFailResults;
        template.IncludeNotes = IncludeNotes ?? template.IncludeNotes;
        template.IncludeRemarks = IncludeRemarks ?? template.IncludeRemarks;
        template.IncludePhotos = IncludePhotos ?? template.IncludePhotos;
        template.IncludeVideoLinks = IncludeVideoLinks ?? template.IncludeVideoLinks;
        template.IncludeDefects = IncludeDefects ?? template.IncludeDefects;
        template.IncludeCorrectiveActions = IncludeCorrectiveActions ?? template.IncludeCorrectiveActions;
        template.IncludeApprovalHistory = IncludeApprovalHistory ?? template.IncludeApprovalHistory;
        template.IncludeSignatures = IncludeSignatures ?? template.IncludeSignatures;
        template.IncludeStamps = IncludeStamps ?? template.IncludeStamps;
        template.IncludeAuditDetails = IncludeAuditDetails ?? template.IncludeAuditDetails;
        template.Orientation = Orientation ?? string.Empty;
        template.PageSize = PageSize ?? string.Empty;
        template.Header = Header ?? string.Empty;
        template.Footer = Footer ?? string.Empty;
        template.ShowPageNumber = ShowPageNumber ?? template.ShowPageNumber;
        template.Watermark = Watermark ?? string.Empty;
        template.ConfidentialityLabel = ConfidentialityLabel ?? string.Empty;
        template.PhotoSize = PhotoSize ?? string.Empty;
        template.NumberOfPhotosPerPage = NumberOfPhotosPerPage ?? template.NumberOfPhotosPerPage;
        template.IncludeFailedTasksOnly = IncludeFailedTasksOnly ?? template.IncludeFailedTasksOnly;
        template.IncludeCompleteInspection = IncludeCompleteInspection ?? template.IncludeCompleteInspection;
        template.IncludePreviousInspectionComparison = IncludePreviousInspectionComparison ?? template.IncludePreviousInspectionComparison;
        template.IsActive = IsActive ?? template.IsActive;
        template.Status = Status;
        template.UpdatedBy = UpdatedBy;
        template.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record ReportTemplateResponse(
    string Id,
    string ReportTemplateCode,
    string ReportTitle,
    string ReportNumberFormat,
    bool IncludeOrganizationLogo,
    bool IncludeOrganizationDetails,
    bool IncludeSiteDetails,
    bool IncludeWorkOrderDetails,
    bool IncludeAssetDetails,
    bool IncludeExecutiveSummary,
    bool IncludeInspectorDetails,
    bool IncludeAssetSummary,
    bool IncludeTaskResponses,
    bool IncludePassOrFailResults,
    bool IncludeNotes,
    bool IncludeRemarks,
    bool IncludePhotos,
    bool IncludeVideoLinks,
    bool IncludeDefects,
    bool IncludeCorrectiveActions,
    bool IncludeApprovalHistory,
    bool IncludeSignatures,
    bool IncludeStamps,
    bool IncludeAuditDetails,
    string Orientation,
    string PageSize,
    string Header,
    string Footer,
    bool ShowPageNumber,
    string Watermark,
    string ConfidentialityLabel,
    string PhotoSize,
    int NumberOfPhotosPerPage,
    bool IncludeFailedTasksOnly,
    bool IncludeCompleteInspection,
    bool IncludePreviousInspectionComparison,
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
    public static ReportTemplateResponse FromEntity(ReportTemplateEntity template)
    {
        return new ReportTemplateResponse(
            template.Id,
            template.ReportTemplateCode,
            template.ReportTitle,
            template.ReportNumberFormat,
            template.IncludeOrganizationLogo,
            template.IncludeOrganizationDetails,
            template.IncludeSiteDetails,
            template.IncludeWorkOrderDetails,
            template.IncludeAssetDetails,
            template.IncludeExecutiveSummary,
            template.IncludeInspectorDetails,
            template.IncludeAssetSummary,
            template.IncludeTaskResponses,
            template.IncludePassOrFailResults,
            template.IncludeNotes,
            template.IncludeRemarks,
            template.IncludePhotos,
            template.IncludeVideoLinks,
            template.IncludeDefects,
            template.IncludeCorrectiveActions,
            template.IncludeApprovalHistory,
            template.IncludeSignatures,
            template.IncludeStamps,
            template.IncludeAuditDetails,
            template.Orientation,
            template.PageSize,
            template.Header,
            template.Footer,
            template.ShowPageNumber,
            template.Watermark,
            template.ConfidentialityLabel,
            template.PhotoSize,
            template.NumberOfPhotosPerPage,
            template.IncludeFailedTasksOnly,
            template.IncludeCompleteInspection,
            template.IncludePreviousInspectionComparison,
            template.IsActive,
            template.Status,
            template.CreatedBy,
            template.CreatedAt,
            template.UpdatedBy,
            template.UpdatedAt,
            template.ClientId,
            template.TenantId,
            template.IsDeleted);
    }
}
