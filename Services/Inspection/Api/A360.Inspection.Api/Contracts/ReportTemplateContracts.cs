using A360.Inspection.Domain.Entities;
using ReportTemplateEntity = A360.Inspection.Domain.Entities.ReportTemplate;

namespace A360.Inspection.Api.Contracts;

/// <summary>Editable fields shared by the create and update requests for ReportTemplate.</summary>
public abstract record ReportTemplateFields
{
    public string? ReportTitle { get; init; }

    public string? InspectionTypeName { get; init; }

    public string? Format { get; init; }

    public string? PageSize { get; init; }

    public string? Orientation { get; init; }

    public string? Language { get; init; }

    public string? AccentColor { get; init; }

    public string? VersionLabel { get; init; }

    public string? ReportNumberFormat { get; init; }

    public List<ReportSection>? Sections { get; init; }

    public bool? IncludePassedItems { get; init; }

    public bool? IncludePhotos { get; init; }

    public bool? ShowAcceptanceCriteria { get; init; }

    public bool? ShowFindingStatus { get; init; }

    public bool? IncludeSignatures { get; init; }

    public bool? IncludeInternalNotes { get; init; }

    public bool? IsActive { get; init; }

    public bool? IncludeOrganizationLogo { get; init; }

    public bool? IncludeOrganizationDetails { get; init; }

    public bool? IncludeSiteDetails { get; init; }

    public bool? IncludeWorkOrderDetails { get; init; }

    public bool? IncludeAssetDetails { get; init; }

    public bool? IncludeExecutiveSummary { get; init; }

    public bool? IncludeInspectorDetails { get; init; }

    public bool? IncludeAssetSummary { get; init; }

    public bool? IncludeTaskResponses { get; init; }

    public bool? IncludePassOrFailResults { get; init; }

    public bool? IncludeNotes { get; init; }

    public bool? IncludeRemarks { get; init; }

    public bool? IncludeVideoLinks { get; init; }

    public bool? IncludeDefects { get; init; }

    public bool? IncludeCorrectiveActions { get; init; }

    public bool? IncludeApprovalHistory { get; init; }

    public bool? IncludeStamps { get; init; }

    public bool? IncludeAuditDetails { get; init; }

    public string? Header { get; init; }

    public string? Footer { get; init; }

    public bool? ShowPageNumber { get; init; }

    public string? Watermark { get; init; }

    public string? ConfidentialityLabel { get; init; }

    public string? PhotoSize { get; init; }

    public int? NumberOfPhotosPerPage { get; init; }

    public bool? IncludeFailedTasksOnly { get; init; }

    public bool? IncludeCompleteInspection { get; init; }

    public bool? IncludePreviousInspectionComparison { get; init; }

    public string? Status { get; init; }

    protected void ApplyFields(ReportTemplateEntity template)
    {
        template.ReportTitle = ReportTitle ?? template.ReportTitle;
        template.InspectionTypeName = InspectionTypeName ?? template.InspectionTypeName;
        template.Format = Format ?? template.Format;
        template.PageSize = PageSize ?? template.PageSize;
        template.Orientation = Orientation ?? template.Orientation;
        template.Language = Language ?? template.Language;
        template.AccentColor = AccentColor ?? template.AccentColor;
        template.VersionLabel = VersionLabel ?? template.VersionLabel;
        template.ReportNumberFormat = ReportNumberFormat ?? template.ReportNumberFormat;
        template.Sections = Sections ?? template.Sections;
        template.IncludePassedItems = IncludePassedItems ?? template.IncludePassedItems;
        template.IncludePhotos = IncludePhotos ?? template.IncludePhotos;
        template.ShowAcceptanceCriteria = ShowAcceptanceCriteria ?? template.ShowAcceptanceCriteria;
        template.ShowFindingStatus = ShowFindingStatus ?? template.ShowFindingStatus;
        template.IncludeSignatures = IncludeSignatures ?? template.IncludeSignatures;
        template.IncludeInternalNotes = IncludeInternalNotes ?? template.IncludeInternalNotes;
        template.IsActive = IsActive ?? template.IsActive;
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
        template.IncludeVideoLinks = IncludeVideoLinks ?? template.IncludeVideoLinks;
        template.IncludeDefects = IncludeDefects ?? template.IncludeDefects;
        template.IncludeCorrectiveActions = IncludeCorrectiveActions ?? template.IncludeCorrectiveActions;
        template.IncludeApprovalHistory = IncludeApprovalHistory ?? template.IncludeApprovalHistory;
        template.IncludeStamps = IncludeStamps ?? template.IncludeStamps;
        template.IncludeAuditDetails = IncludeAuditDetails ?? template.IncludeAuditDetails;
        template.Header = Header ?? template.Header;
        template.Footer = Footer ?? template.Footer;
        template.ShowPageNumber = ShowPageNumber ?? template.ShowPageNumber;
        template.Watermark = Watermark ?? template.Watermark;
        template.ConfidentialityLabel = ConfidentialityLabel ?? template.ConfidentialityLabel;
        template.PhotoSize = PhotoSize ?? template.PhotoSize;
        template.NumberOfPhotosPerPage = NumberOfPhotosPerPage ?? template.NumberOfPhotosPerPage;
        template.IncludeFailedTasksOnly = IncludeFailedTasksOnly ?? template.IncludeFailedTasksOnly;
        template.IncludeCompleteInspection = IncludeCompleteInspection ?? template.IncludeCompleteInspection;
        template.IncludePreviousInspectionComparison = IncludePreviousInspectionComparison ?? template.IncludePreviousInspectionComparison;
    }
}

public sealed record CreateReportTemplateRequest : ReportTemplateFields
{
    public string? CreatedBy { get; init; }

    public string? ClientId { get; init; }

    public string? TenantId { get; init; }

    public ReportTemplateEntity ToEntity(string reportTemplateCode)
    {
        var template = new ReportTemplateEntity
        {
            ReportTemplateCode = reportTemplateCode,
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };

        ApplyFields(template);
        return template;
    }
}

public sealed record UpdateReportTemplateRequest : ReportTemplateFields
{
    public string? UpdatedBy { get; init; }

    public void ApplyTo(ReportTemplateEntity template)
    {
        ApplyFields(template);
        template.Status = Status ?? template.Status;
        template.UpdatedBy = UpdatedBy;
        template.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record ReportTemplateResponse : AuditResponse
{
    private ReportTemplateResponse(ReportTemplateEntity template)
        : base(template)
    {
    }

    public string ReportTemplateCode { get; init; } = string.Empty;

    public string ReportTitle { get; init; } = default!;

    public string InspectionTypeName { get; init; } = default!;

    public string Format { get; init; } = default!;

    public string PageSize { get; init; } = default!;

    public string Orientation { get; init; } = default!;

    public string Language { get; init; } = default!;

    public string AccentColor { get; init; } = default!;

    public string VersionLabel { get; init; } = default!;

    public string ReportNumberFormat { get; init; } = default!;

    public List<ReportSection> Sections { get; init; } = default!;

    public bool IncludePassedItems { get; init; }

    public bool IncludePhotos { get; init; }

    public bool ShowAcceptanceCriteria { get; init; }

    public bool ShowFindingStatus { get; init; }

    public bool IncludeSignatures { get; init; }

    public bool IncludeInternalNotes { get; init; }

    public bool IsActive { get; init; }

    public bool IncludeOrganizationLogo { get; init; }

    public bool IncludeOrganizationDetails { get; init; }

    public bool IncludeSiteDetails { get; init; }

    public bool IncludeWorkOrderDetails { get; init; }

    public bool IncludeAssetDetails { get; init; }

    public bool IncludeExecutiveSummary { get; init; }

    public bool IncludeInspectorDetails { get; init; }

    public bool IncludeAssetSummary { get; init; }

    public bool IncludeTaskResponses { get; init; }

    public bool IncludePassOrFailResults { get; init; }

    public bool IncludeNotes { get; init; }

    public bool IncludeRemarks { get; init; }

    public bool IncludeVideoLinks { get; init; }

    public bool IncludeDefects { get; init; }

    public bool IncludeCorrectiveActions { get; init; }

    public bool IncludeApprovalHistory { get; init; }

    public bool IncludeStamps { get; init; }

    public bool IncludeAuditDetails { get; init; }

    public string Header { get; init; } = default!;

    public string Footer { get; init; } = default!;

    public bool ShowPageNumber { get; init; }

    public string Watermark { get; init; } = default!;

    public string ConfidentialityLabel { get; init; } = default!;

    public string PhotoSize { get; init; } = default!;

    public int NumberOfPhotosPerPage { get; init; }

    public bool IncludeFailedTasksOnly { get; init; }

    public bool IncludeCompleteInspection { get; init; }

    public bool IncludePreviousInspectionComparison { get; init; }

    public static ReportTemplateResponse FromEntity(ReportTemplateEntity template)
    {
        return new ReportTemplateResponse(template)
        {
            ReportTemplateCode = template.ReportTemplateCode,
            ReportTitle = template.ReportTitle,
            InspectionTypeName = template.InspectionTypeName,
            Format = template.Format,
            PageSize = template.PageSize,
            Orientation = template.Orientation,
            Language = template.Language,
            AccentColor = template.AccentColor,
            VersionLabel = template.VersionLabel,
            ReportNumberFormat = template.ReportNumberFormat,
            Sections = template.Sections,
            IncludePassedItems = template.IncludePassedItems,
            IncludePhotos = template.IncludePhotos,
            ShowAcceptanceCriteria = template.ShowAcceptanceCriteria,
            ShowFindingStatus = template.ShowFindingStatus,
            IncludeSignatures = template.IncludeSignatures,
            IncludeInternalNotes = template.IncludeInternalNotes,
            IsActive = template.IsActive,
            IncludeOrganizationLogo = template.IncludeOrganizationLogo,
            IncludeOrganizationDetails = template.IncludeOrganizationDetails,
            IncludeSiteDetails = template.IncludeSiteDetails,
            IncludeWorkOrderDetails = template.IncludeWorkOrderDetails,
            IncludeAssetDetails = template.IncludeAssetDetails,
            IncludeExecutiveSummary = template.IncludeExecutiveSummary,
            IncludeInspectorDetails = template.IncludeInspectorDetails,
            IncludeAssetSummary = template.IncludeAssetSummary,
            IncludeTaskResponses = template.IncludeTaskResponses,
            IncludePassOrFailResults = template.IncludePassOrFailResults,
            IncludeNotes = template.IncludeNotes,
            IncludeRemarks = template.IncludeRemarks,
            IncludeVideoLinks = template.IncludeVideoLinks,
            IncludeDefects = template.IncludeDefects,
            IncludeCorrectiveActions = template.IncludeCorrectiveActions,
            IncludeApprovalHistory = template.IncludeApprovalHistory,
            IncludeStamps = template.IncludeStamps,
            IncludeAuditDetails = template.IncludeAuditDetails,
            Header = template.Header,
            Footer = template.Footer,
            ShowPageNumber = template.ShowPageNumber,
            Watermark = template.Watermark,
            ConfidentialityLabel = template.ConfidentialityLabel,
            PhotoSize = template.PhotoSize,
            NumberOfPhotosPerPage = template.NumberOfPhotosPerPage,
            IncludeFailedTasksOnly = template.IncludeFailedTasksOnly,
            IncludeCompleteInspection = template.IncludeCompleteInspection,
            IncludePreviousInspectionComparison = template.IncludePreviousInspectionComparison
        };
    }
}
