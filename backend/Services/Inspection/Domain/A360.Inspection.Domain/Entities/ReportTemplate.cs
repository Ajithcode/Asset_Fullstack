using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Inspection.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class ReportTemplate : BaseEntity
{
    [BsonElement("report_template_code")]
    public string ReportTemplateCode { get; set; } = string.Empty;

    [BsonElement("report_title")]
    public string ReportTitle { get; set; } = string.Empty;

    [BsonElement("report_number_format")]
    public string ReportNumberFormat { get; set; } = string.Empty;

    [BsonElement("include_organization_logo")]
    public bool IncludeOrganizationLogo { get; set; }

    [BsonElement("include_organization_details")]
    public bool IncludeOrganizationDetails { get; set; }

    [BsonElement("include_site_details")]
    public bool IncludeSiteDetails { get; set; }

    [BsonElement("include_work_order_details")]
    public bool IncludeWorkOrderDetails { get; set; }

    [BsonElement("include_asset_details")]
    public bool IncludeAssetDetails { get; set; }

    [BsonElement("include_executive_summary")]
    public bool IncludeExecutiveSummary { get; set; }

    [BsonElement("include_inspector_details")]
    public bool IncludeInspectorDetails { get; set; }

    [BsonElement("include_asset_summary")]
    public bool IncludeAssetSummary { get; set; }

    [BsonElement("include_task_responses")]
    public bool IncludeTaskResponses { get; set; }

    [BsonElement("include_pass_or_fail_results")]
    public bool IncludePassOrFailResults { get; set; }

    [BsonElement("include_notes")]
    public bool IncludeNotes { get; set; }

    [BsonElement("include_remarks")]
    public bool IncludeRemarks { get; set; }

    [BsonElement("include_photos")]
    public bool IncludePhotos { get; set; }

    [BsonElement("include_video_links")]
    public bool IncludeVideoLinks { get; set; }

    [BsonElement("include_defects")]
    public bool IncludeDefects { get; set; }

    [BsonElement("include_corrective_actions")]
    public bool IncludeCorrectiveActions { get; set; }

    [BsonElement("include_approval_history")]
    public bool IncludeApprovalHistory { get; set; }

    [BsonElement("include_signatures")]
    public bool IncludeSignatures { get; set; }

    [BsonElement("include_stamps")]
    public bool IncludeStamps { get; set; }

    [BsonElement("include_audit_details")]
    public bool IncludeAuditDetails { get; set; }

    [BsonElement("orientation")]
    public string Orientation { get; set; } = string.Empty;

    [BsonElement("page_size")]
    public string PageSize { get; set; } = string.Empty;

    [BsonElement("header")]
    public string Header { get; set; } = string.Empty;

    [BsonElement("footer")]
    public string Footer { get; set; } = string.Empty;

    [BsonElement("show_page_number")]
    public bool ShowPageNumber { get; set; }

    [BsonElement("watermark")]
    public string Watermark { get; set; } = string.Empty;

    [BsonElement("confidentiality_label")]
    public string ConfidentialityLabel { get; set; } = string.Empty;

    [BsonElement("photo_size")]
    public string PhotoSize { get; set; } = string.Empty;

    [BsonElement("number_of_photos_per_page")]
    public int NumberOfPhotosPerPage { get; set; }

    [BsonElement("include_failed_tasks_only")]
    public bool IncludeFailedTasksOnly { get; set; }

    [BsonElement("include_complete_inspection")]
    public bool IncludeCompleteInspection { get; set; }

    [BsonElement("include_previous_inspection_comparison")]
    public bool IncludePreviousInspectionComparison { get; set; }

    [BsonElement("is_active")]
    public bool IsActive { get; set; } = true;
}
