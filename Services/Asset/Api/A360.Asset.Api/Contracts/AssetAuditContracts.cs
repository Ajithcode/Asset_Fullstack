using A360.Asset.Domain.Entities;
using A360.Asset.Repository.Repositories;

namespace A360.Asset.Api.Contracts;

public static class AssetAuditOptions
{
    public static readonly OptionList AuditTypes =
        new("Periodic", "Ad-hoc", "Annual", "Compliance", "Spot Check", "Preventive");

    public const string AllInLocation = "All In Location";
    public const string SelectedAssets = "Selected Assets";
    public const string TagRange = "Tag Range";
    public const string Filter = "Filter";

    public static readonly OptionList SelectionMethods = new(AllInLocation, SelectedAssets, TagRange, Filter);

    public static readonly OptionList CriterionTypes =
        new("Checklist", "Condition Assessment", "Compliance", "Documentation", "Custom");

    public const string AllAssets = "All Assets";

    public static readonly OptionList AppliesTo = new(AllAssets, SelectedAssets);

    public static readonly OptionList CheckMethods =
        new("Visual Inspection", "Scan Barcode / RFID", "Compare with record", "Functional Test", "Measurement", "Document Review", "Photo Evidence");

    public const string LeadAuditor = "Lead Auditor";

    public static readonly OptionList Roles = new(LeadAuditor, "Auditor", "Observer", "Reviewer");

    // Roles that are given assets in the plan; Observers and Reviewers only follow along.
    public static readonly OptionList PlannedRoles = new(LeadAuditor, "Auditor");

    public const string Weekdays = "Weekdays (Mon - Fri)";
    public const string AllDays = "All Days";
    public const string CustomDays = "Custom";

    public static readonly OptionList WorkingDays = new(Weekdays, AllDays, CustomDays);

    public static readonly OptionList DaysOfWeek =
        new("Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday");

    public const string ByLocation = "By Location";
    public const string ByAssetCategory = "By Asset Category";
    public const string CustomOrder = "Custom Order";

    public static readonly OptionList LocationSequences = new(ByLocation, ByAssetCategory, CustomOrder);

    public static readonly OptionList PlanStatuses =
        new(AssetAuditRepository.ScheduledStatus, AssetAuditRepository.InProgressStatus, AssetAuditRepository.CompletedStatus);

    public static readonly OptionList ListStatuses =
        new(AssetAuditRepository.DraftStatus, AssetAuditRepository.ScheduledStatus, AssetAuditRepository.InProgressStatus,
            AssetAuditRepository.CompletedStatus, AssetAuditRepository.OverdueStatus);
}

/// <summary>The Audit Criteria checkboxes on step 1.</summary>
public static class AssetAuditCriteriaOptions
{
    public const string PhysicalExistence = "PhysicalExistence";
    public const string ConditionCheck = "ConditionCheck";
    public const string TagVerification = "TagVerification";
    public const string AccessoriesCheck = "AccessoriesCheck";
    public const string ComplianceDocumentation = "ComplianceDocumentation";
    public const string UpdateLocationAssignment = "UpdateLocationAssignment";
    public const string PhotoCapture = "PhotoCapture";
    public const string Other = "Other";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [PhysicalExistence] = "Physical existence (asset present at location)",
        [ConditionCheck] = "Condition check (visual inspection)",
        [TagVerification] = "Asset tag / serial number verification",
        [AccessoriesCheck] = "Accessories / components check",
        [ComplianceDocumentation] = "Compliance and documentation check",
        [UpdateLocationAssignment] = "Update location / assignment if changed",
        [PhotoCapture] = "Photo capture (mandatory)",
        [Other] = "Other (specify)"
    };

    public static string? Normalize(string? option) =>
        Labels.Keys.FirstOrDefault(key => string.Equals(key, option?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <param name="OptionKey">The step 1 checkbox that adds this criterion to a new audit, if any.</param>
public sealed record AssetAuditCriterionTemplate(
    string Key,
    string Name,
    string Description,
    string Category,
    string Type,
    string CheckMethod,
    bool IsMandatory,
    string? OptionKey)
{
    public AssetAuditCriterion ToCriterion(string criterionId) => new()
    {
        CriterionId = criterionId,
        Name = Name,
        Description = Description,
        Category = Category,
        Type = Type,
        CheckMethod = CheckMethod,
        AppliesTo = AssetAuditOptions.AllAssets,
        IsMandatory = IsMandatory
    };
}

/// <summary>Load from Template: the built-in criteria, by tab.</summary>
public static class AssetAuditCriterionTemplates
{
    public static readonly IReadOnlyList<AssetAuditCriterionTemplate> All =
    [
        new("physical-presence", "Physical Presence", "Asset is physically present at the recorded location.", "Existence", "Checklist", "Visual Inspection", true, AssetAuditCriteriaOptions.PhysicalExistence),
        new("asset-identification", "Asset Identification", "Asset tag / serial number matches the register.", "Identification", "Checklist", "Scan Barcode / RFID", true, AssetAuditCriteriaOptions.TagVerification),
        new("location-verification", "Location Verification", "Actual location matches the recorded location and assignment.", "Location", "Checklist", "Compare with record", true, AssetAuditCriteriaOptions.UpdateLocationAssignment),
        new("physical-condition", "Physical Condition", "No visible damage, corrosion or missing parts.", "Condition", "Checklist", "Visual Inspection", true, AssetAuditCriteriaOptions.ConditionCheck),
        new("accessories-components", "Accessories & Components", "Accessories and components are present and fitted.", "Components", "Checklist", "Visual Inspection", false, AssetAuditCriteriaOptions.AccessoriesCheck),
        new("maintenance-tag", "Maintenance Tag", "Maintenance / inspection tag is present and current.", "Compliance", "Checklist", "Visual Inspection", false, AssetAuditCriteriaOptions.ComplianceDocumentation),

        new("condition-rating", "Overall Condition Rating", "Rate the overall condition (Excellent, Good, Fair, Poor).", "Condition", "Condition Assessment", "Visual Inspection", false, null),
        new("wear-damage", "Wear & Damage", "Record wear, leaks, cracks or other damage.", "Condition", "Condition Assessment", "Visual Inspection", false, null),
        new("operational-status", "Operational Status", "Asset starts and runs as expected.", "Condition", "Condition Assessment", "Functional Test", false, null),
        new("cleanliness", "Cleanliness & Housekeeping", "Asset and its surroundings are clean and unobstructed.", "Condition", "Condition Assessment", "Visual Inspection", false, null),

        new("safety-certificate", "Safety Certificate Valid", "Safety / calibration certificate is valid.", "Compliance", "Compliance", "Document Review", false, null),
        new("statutory-inspection", "Statutory Inspection Up to Date", "Statutory inspection has been carried out on time.", "Compliance", "Compliance", "Document Review", false, null),
        new("regulatory-labels", "Regulatory Labels Present", "Required warning and rating labels are fitted and legible.", "Compliance", "Compliance", "Visual Inspection", false, null),

        new("purchase-documents", "Warranty / Purchase Documents", "Invoice, warranty and purchase records are on file.", "Documentation", "Documentation", "Document Review", false, null),
        new("maintenance-records", "Maintenance Records Available", "Maintenance history is recorded and up to date.", "Documentation", "Documentation", "Document Review", false, null)
    ];
}

// ---- Requests ----

/// <summary>Step 1: Basic Information, Scope &amp; Assets and the Audit Criteria checkboxes.</summary>
/// <param name="AuditReference">Optional; generated as e.g. "AUD-2025-025" when left empty.</param>
/// <param name="CriteriaOptions">Keys from AssetAuditCriteriaOptions, e.g. "PhysicalExistence".</param>
/// <param name="OtherCriteriaDetails">Required when "Other" is ticked.</param>
public sealed record AssetAuditBasicInformationRequest(
    string? AuditName,
    string? AuditType,
    string? AuditReference,
    string? Description,
    AssetAuditScopeRequest? Scope,
    IReadOnlyCollection<string>? CriteriaOptions,
    string? OtherCriteriaDetails,
    string? CreatedBy,
    string? ClientId,
    string? TenantId);

/// <param name="LocationId">Any level of the location hierarchy, e.g. the building "Main Plant - Block A".</param>
/// <param name="DepartmentName">Leave empty for all departments; assets are matched on this name.</param>
/// <param name="CategoryName">Leave empty for all categories; assets are matched on this name.</param>
/// <param name="SelectionMethod">One of AssetAuditOptions.SelectionMethods.</param>
/// <param name="TagFrom">"By asset tag range": first and last tag.</param>
/// <param name="AssetStatus">"By filter / criteria": asset status, type and criticality.</param>
public sealed record AssetAuditScopeRequest(
    string? LocationId,
    string? LocationName,
    string? DepartmentId,
    string? DepartmentName,
    string? CategoryId,
    string? CategoryName,
    string? SelectionMethod,
    string? TagFrom,
    string? TagTo,
    string? AssetStatus,
    string? AssetType,
    string? Criticality)
{
    public AssetAuditScope ToEntity()
    {
        var method = AssetAuditOptions.SelectionMethods.NormalizeOrKeep(SelectionMethod);
        var isTagRange = method == AssetAuditOptions.TagRange;
        var isFilter = method == AssetAuditOptions.Filter;

        return new AssetAuditScope
        {
            LocationId = ContractText.TrimOrNull(LocationId),
            LocationName = ContractText.TrimOrNull(LocationName),
            DepartmentId = ContractText.TrimOrNull(DepartmentId),
            DepartmentName = ContractText.TrimOrNull(DepartmentName),
            CategoryId = ContractText.TrimOrNull(CategoryId),
            CategoryName = ContractText.TrimOrNull(CategoryName),
            SelectionMethod = method,
            TagFrom = isTagRange ? ContractText.TrimOrNull(TagFrom) : null,
            TagTo = isTagRange ? ContractText.TrimOrNull(TagTo) : null,
            AssetStatus = isFilter ? ContractText.TrimOrNull(AssetStatus) : null,
            AssetType = isFilter ? ContractText.TrimOrNull(AssetType) : null,
            Criticality = isFilter ? ContractText.TrimOrNull(Criticality) : null
        };
    }
}

/// <summary>Step 2: the ticked assets and, for each, the sensors / components to check.</summary>
public sealed record AssetAuditAssetsRequest(IReadOnlyCollection<AssetAuditAssetSelectionRequest>? Assets);

/// <param name="ComponentIds">AssetComponent ids of this asset; empty to check none separately.</param>
public sealed record AssetAuditAssetSelectionRequest(string? AssetRegistrationId, IReadOnlyCollection<string>? ComponentIds);

/// <summary>Step 3: every criterion (all tabs) and the Criteria Settings. The list replaces the saved one.</summary>
public sealed record AssetAuditCriteriaRequest(
    IReadOnlyCollection<AssetAuditCriterionRequest>? Criteria,
    AssetAuditCriteriaSettingsRequest? Settings);

/// <param name="CriterionId">Keep the id of an existing criterion; leave empty for a new one.</param>
/// <param name="Type">The tab: one of AssetAuditOptions.CriterionTypes.</param>
/// <param name="AppliesTo">"All Assets" or "Selected Assets" (then AssetRegistrationIds).</param>
/// <param name="IsMandatory">Empty uses Settings.DefaultMandatory.</param>
public sealed record AssetAuditCriterionRequest(
    string? CriterionId,
    string? Name,
    string? Description,
    string? Category,
    string? Type,
    string? CheckMethod,
    string? AppliesTo,
    IReadOnlyCollection<string>? AssetRegistrationIds,
    bool? IsMandatory);

public sealed record AssetAuditCriteriaSettingsRequest(
    bool CapturePhotos,
    bool AllowComments,
    bool DefaultMandatory,
    bool ShowPassFail,
    bool EnableScoring,
    bool AllowAttachments)
{
    public AssetAuditCriteriaSettings ToEntity() => new()
    {
        CapturePhotos = CapturePhotos,
        AllowComments = AllowComments,
        DefaultMandatory = DefaultMandatory,
        ShowPassFail = ShowPassFail,
        EnableScoring = EnableScoring,
        AllowAttachments = AllowAttachments
    };
}

/// <summary>Step 4: Assign Audit Team, Schedule Audit, Audit Plan and Notifications.</summary>
/// <param name="Plan">
/// The plan as edited on screen. Leave empty with Schedule.AutoPlan to have it generated
/// (the same as Regenerate Plan).
/// </param>
public sealed record AssetAuditScheduleRequest(
    IReadOnlyCollection<AssetAuditTeamMemberRequest>? Team,
    AssetAuditScheduleDetailsRequest? Schedule,
    IReadOnlyCollection<AssetAuditPlanEntryRequest>? Plan,
    AssetAuditNotificationsRequest? Notifications);

/// <param name="UserId">The user picked in the search box.</param>
/// <param name="Role">One of AssetAuditOptions.Roles.</param>
public sealed record AssetAuditTeamMemberRequest(
    string? UserId,
    string? Name,
    string? Email,
    string? Role,
    string? Department)
{
    public AssetAuditTeamMember ToEntity() => new()
    {
        UserId = UserId?.Trim() ?? string.Empty,
        Name = Name?.Trim() ?? string.Empty,
        Email = ContractText.TrimOrNull(Email),
        Role = AssetAuditOptions.Roles.NormalizeOrKeep(Role) ?? string.Empty,
        Department = ContractText.TrimOrNull(Department)
    };
}

/// <param name="WorkingDays">One of AssetAuditOptions.WorkingDays; "Custom" uses CustomDays.</param>
/// <param name="DailyStartTime">"HH:mm", e.g. "09:00".</param>
/// <param name="LocationSequence">One of AssetAuditOptions.LocationSequences.</param>
public sealed record AssetAuditScheduleDetailsRequest(
    DateTime? StartDate,
    DateTime? EndDate,
    string? WorkingDays,
    IReadOnlyCollection<string>? CustomDays,
    string? DailyStartTime,
    string? DailyEndTime,
    string? LocationSequence,
    bool AutoPlan = true)
{
    public AssetAuditSchedule ToEntity()
    {
        var workingDays = AssetAuditOptions.WorkingDays.NormalizeOrKeep(WorkingDays);
        return new AssetAuditSchedule
        {
            StartDate = ContractText.DateOnly(StartDate),
            EndDate = ContractText.DateOnly(EndDate),
            WorkingDays = workingDays,
            CustomDays = workingDays == AssetAuditOptions.CustomDays
                ? (CustomDays ?? []).Select(AssetAuditOptions.DaysOfWeek.NormalizeOrKeep).OfType<string>().Distinct().ToList()
                : [],
            DailyStartTime = ContractText.TrimOrNull(DailyStartTime),
            DailyEndTime = ContractText.TrimOrNull(DailyEndTime),
            LocationSequence = AssetAuditOptions.LocationSequences.NormalizeOrKeep(LocationSequence),
            AutoPlan = AutoPlan
        };
    }
}

public sealed record AssetAuditPlanEntryRequest(
    DateTime? Date,
    string? Location,
    IReadOnlyCollection<string>? AssetRegistrationIds,
    string? AssignedToUserId,
    string? Status);

public sealed record AssetAuditNotificationsRequest(
    bool NotifyTeamOnAssignment,
    bool SendScheduleReminder,
    bool NotifyOnCompletion,
    bool NotifyOnScheduleChange)
{
    public AssetAuditNotifications ToEntity() => new()
    {
        NotifyTeamOnAssignment = NotifyTeamOnAssignment,
        SendScheduleReminder = SendScheduleReminder,
        NotifyOnCompletion = NotifyOnCompletion,
        NotifyOnScheduleChange = NotifyOnScheduleChange
    };
}

/// <summary>Step 5: Attachments (already uploaded through the Media service) and Notes &amp; Instructions.</summary>
public sealed record AssetAuditReviewRequest(
    IReadOnlyCollection<AssetAuditAttachmentRequest>? Attachments,
    string? Notes);

/// <param name="FileType">e.g. "Template", "Image", "Document".</param>
public sealed record AssetAuditAttachmentRequest(
    string? FileName,
    string? FileUrl,
    string? FileType,
    string? ContentType,
    long? FileSize,
    string? UploadedBy,
    DateTime? UploadedAt)
{
    public AssetAuditAttachment ToEntity(string? currentUser) => new()
    {
        FileName = FileName?.Trim() ?? string.Empty,
        FileUrl = FileUrl?.Trim() ?? string.Empty,
        FileType = ContractText.TrimOrNull(FileType),
        ContentType = ContractText.TrimOrNull(ContentType),
        FileSize = FileSize,
        UploadedBy = ContractText.FirstNonEmpty(UploadedBy, currentUser),
        UploadedAt = UploadedAt ?? DateTime.UtcNow
    };
}

/// <param name="DiscrepancyCount">Findings: assets missing, damaged, moved or otherwise not as recorded.</param>
public sealed record CompleteAssetAuditRequest(int? DiscrepancyCount, string? Remarks);

public sealed record DeleteAssetAuditsRequest(IReadOnlyCollection<string>? Ids);

// ---- Responses ----

/// <summary>Fixed options for the wizard's dropdowns, radio buttons and tabs.</summary>
public sealed record AssetAuditOptionsResponse(
    IEnumerable<string> AuditTypes,
    IEnumerable<string> Statuses,
    IEnumerable<string> SelectionMethods,
    IReadOnlyDictionary<string, string> CriteriaOptions,
    IEnumerable<string> CriterionTypes,
    IEnumerable<string> CheckMethods,
    IEnumerable<string> AppliesTo,
    IEnumerable<string> Roles,
    IEnumerable<string> WorkingDays,
    IEnumerable<string> DaysOfWeek,
    IEnumerable<string> LocationSequences);

/// <summary>The Audit Summary panel.</summary>
/// <param name="TotalAssets">Assets in scope.</param>
/// <param name="SelectedAssets">Assets the audit covers.</param>
public sealed record AssetAuditSummary(
    string AuditName,
    string? AuditType,
    string? Location,
    string Department,
    string AssetCategory,
    int TotalAssets,
    int SelectedAssets,
    DateTime? ScheduledFrom,
    DateTime? ScheduledTo,
    string? Status,
    int CriteriaCount,
    int MandatoryCriteriaCount,
    int OptionalCriteriaCount,
    int TeamCount,
    string? LeadAuditor);

/// <param name="Status">As shown: Overdue for an open audit past its end date.</param>
/// <param name="SelectedAssetCount">Assets in the audit (for a draft not using "Selected Assets", the scope is added on submit).</param>
public sealed record AssetAuditResponse(
    string Id,
    string AuditReference,
    string? Status,
    int CurrentStep,
    string AuditName,
    string? AuditType,
    string? Description,
    AssetAuditScope Scope,
    IReadOnlyCollection<string> CriteriaOptions,
    string? OtherCriteriaDetails,
    int SelectedAssetCount,
    IReadOnlyCollection<AssetAuditAsset> Assets,
    IReadOnlyCollection<AssetAuditCriterion> Criteria,
    AssetAuditCriteriaSettings CriteriaSettings,
    IReadOnlyCollection<AssetAuditTeamMember> Team,
    AssetAuditSchedule Schedule,
    IReadOnlyCollection<AssetAuditPlanEntry> Plan,
    AssetAuditNotifications Notifications,
    IReadOnlyCollection<AssetAuditAttachment> Attachments,
    string? Notes,
    AssetAuditSummary Summary,
    string? SubmittedBy,
    DateTime? SubmittedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int? DiscrepancyCount,
    string? CompletionRemarks,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId)
{
    public static AssetAuditResponse FromEntity(AssetAudit audit, DateTime today)
    {
        var status = AssetAuditRepository.DisplayStatus(audit, today);
        var mandatory = audit.Criteria.Count(criterion => criterion.IsMandatory);

        return new AssetAuditResponse(
            audit.Id,
            audit.AuditReference,
            status,
            audit.CurrentStep,
            audit.AuditName,
            audit.AuditType,
            audit.Description,
            audit.Scope,
            audit.CriteriaOptions,
            audit.OtherCriteriaDetails,
            audit.Assets.Count,
            audit.Assets,
            audit.Criteria,
            audit.CriteriaSettings,
            audit.Team,
            audit.Schedule,
            audit.Plan,
            audit.Notifications,
            audit.Attachments,
            audit.Notes,
            new AssetAuditSummary(
                audit.AuditName,
                audit.AuditType,
                audit.Scope.LocationName,
                audit.Scope.DepartmentName ?? "All Departments",
                audit.Scope.CategoryName ?? "All Categories",
                audit.Scope.TotalAssetsInScope,
                audit.Assets.Count,
                audit.Schedule.StartDate,
                audit.Schedule.EndDate,
                status,
                audit.Criteria.Count,
                mandatory,
                audit.Criteria.Count - mandatory,
                audit.Team.Count,
                LeadAuditorName(audit)),
            audit.SubmittedBy,
            audit.SubmittedAt,
            audit.StartedAt,
            audit.CompletedAt,
            audit.DiscrepancyCount,
            audit.CompletionRemarks,
            audit.CreatedBy,
            audit.CreatedAt,
            audit.UpdatedBy,
            audit.UpdatedAt,
            audit.ClientId,
            audit.TenantId);
    }

    public static string? LeadAuditorName(AssetAudit audit) =>
        audit.Team.FirstOrDefault(member => member.Role == AssetAuditOptions.LeadAuditor)?.Name
        ?? audit.Team.FirstOrDefault()?.Name;
}

/// <summary>One row of the Asset Audit list.</summary>
/// <param name="ScheduledDate">The schedule's start date.</param>
/// <param name="AuditedBy">The lead auditor.</param>
/// <param name="DiscrepancyCount">Findings; null until completed.</param>
public sealed record AssetAuditListItemResponse(
    string Id,
    string AuditReference,
    string AuditName,
    string? AuditType,
    string? Location,
    DateTime? ScheduledDate,
    DateTime? EndDate,
    DateTime? CompletedDate,
    string? Status,
    string? AuditedBy,
    int AssetCount,
    int? DiscrepancyCount)
{
    public static AssetAuditListItemResponse FromEntity(AssetAudit audit, DateTime today) => new(
        audit.Id,
        audit.AuditReference,
        audit.AuditName,
        audit.AuditType,
        audit.Scope.LocationName,
        audit.Schedule.StartDate,
        audit.Schedule.EndDate,
        audit.CompletedAt,
        AssetAuditRepository.DisplayStatus(audit, today),
        AssetAuditResponse.LeadAuditorName(audit),
        audit.Assets.Count,
        audit.DiscrepancyCount);
}

/// <summary>The summary cards; Total leaves out drafts.</summary>
public sealed record AssetAuditCounts(long Total, long Completed, long InProgress, long Scheduled, long Overdue, long Draft)
{
    public static AssetAuditCounts FromStatusCounts(IReadOnlyDictionary<string, long> counts)
    {
        long Count(string status) => counts.GetValueOrDefault(status);

        var draft = Count(AssetAuditRepository.DraftStatus);
        return new AssetAuditCounts(
            counts.Values.Sum() - draft,
            Count(AssetAuditRepository.CompletedStatus),
            Count(AssetAuditRepository.InProgressStatus),
            Count(AssetAuditRepository.ScheduledStatus),
            Count(AssetAuditRepository.OverdueStatus),
            draft);
    }
}

public sealed record AssetAuditListResponse(
    IReadOnlyCollection<AssetAuditListItemResponse> Items,
    long TotalCount,
    AssetAuditCounts Counts,
    int Page,
    int PageSize);

/// <summary>Preview Assets / the assets an audit covers.</summary>
/// <param name="TotalAssets">"Assets to be audited".</param>
/// <param name="IsTruncated">More assets match than an audit can hold.</param>
public sealed record AssetAuditAssetsResponse(
    int TotalAssets,
    bool IsTruncated,
    IReadOnlyCollection<AssetAuditAsset> Items,
    int Page,
    int PageSize);

/// <summary>Import from Excel on step 2.</summary>
/// <param name="NotFound">Tags in the file that match no asset.</param>
public sealed record AssetAuditImportResponse(
    AssetAuditResponse Audit,
    int ImportedCount,
    IReadOnlyCollection<string> NotFound);

/// <summary>Regenerate Plan: the plan and each member's asset count, not yet saved.</summary>
public sealed record AssetAuditPlanResponse(
    IReadOnlyCollection<AssetAuditPlanEntryResponse> Plan,
    IReadOnlyCollection<AssetAuditTeamMember> Team,
    int WorkingDayCount,
    int TotalAssets);

/// <param name="AssetsCount">The Assets Count column.</param>
public sealed record AssetAuditPlanEntryResponse(
    DateTime Date,
    string? Location,
    int AssetsCount,
    IReadOnlyCollection<string> AssetRegistrationIds,
    string? AssignedToUserId,
    string? AssignedToName,
    string Status)
{
    public static AssetAuditPlanEntryResponse FromEntity(AssetAuditPlanEntry entry) => new(
        entry.Date,
        entry.Location,
        entry.AssetRegistrationIds.Count,
        entry.AssetRegistrationIds,
        entry.AssignedToUserId,
        entry.AssignedToName,
        entry.Status);
}
