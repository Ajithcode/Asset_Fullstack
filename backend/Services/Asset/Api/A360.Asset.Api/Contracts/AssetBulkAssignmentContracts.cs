using A360.Asset.Domain.Entities;

namespace A360.Asset.Api.Contracts;

/// <summary>One Asset360 field on the Map Columns step.</summary>
/// <param name="Key">Sent back as the mapping key, e.g. "assetTag".</param>
/// <param name="DataType">"Text", "Lookup" or "Date".</param>
/// <param name="Required">A column must be mapped to it before the rows can be checked.</param>
/// <param name="Aliases">Header texts (letters and digits only, lower case) matched when suggesting a mapping.</param>
public sealed record AssetBulkAssignmentField(
    string Key,
    string Label,
    string DataType,
    bool Required,
    IReadOnlyCollection<string> Aliases,
    Func<AssetBulkAssignmentValues, string?> Get,
    Action<AssetBulkAssignmentValues, string?> Set);

/// <summary>Fields of the bulk assignment file, in template order.</summary>
public static class AssetBulkAssignmentFields
{
    public const string SheetName = "Assignments";
    public const int MaxRows = 1000;
    public const int MaxColumns = 100;
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public const string AssetTag = "assetTag";
    public const string AssetName = "assetName";
    public const string AssetType = "assetType";
    public const string AssignmentType = "assignmentType";
    public const string AssignTo = "assignTo";
    public const string Department = "department";
    public const string Location = "location";
    public const string StartDate = "startDate";
    public const string EndDate = "endDate";
    public const string Purpose = "purpose";
    public const string CostCenter = "costCenter";
    public const string Notes = "notes";

    public static readonly IReadOnlyList<AssetBulkAssignmentField> All =
    [
        new(AssetTag, "Asset Tag / Serial", "Text", true,
            ["assettag", "tag", "serial", "serialno", "serialnumber", "assettagserialnumber", "assettagorserial"],
            values => values.AssetTag, (values, value) => values.AssetTag = value),
        new(AssetName, "Asset Name", "Text", false,
            ["name"],
            values => values.AssetName, (values, value) => values.AssetName = value),
        new(AssetType, "Asset Type", "Lookup", false,
            ["type"],
            values => values.AssetType, (values, value) => values.AssetType = value),
        new(AssignmentType, "Assignment Type", "Lookup", true,
            ["assigntype", "assignmenttarget", "assignedtotype"],
            values => values.AssignmentType, (values, value) => values.AssignmentType = value),
        new(AssignTo, "Assign To", "Lookup", false,
            ["assigntoemployee", "assignee", "assignedto", "employee", "employeename", "employeeid"],
            values => values.AssignTo, (values, value) => values.AssignTo = value),
        new(Department, "Department", "Lookup", false,
            ["dept", "departmentname"],
            values => values.Department, (values, value) => values.Department = value),
        new(Location, "Location / Site", "Lookup", false,
            ["location", "site", "locationsite", "sitelocation"],
            values => values.Location, (values, value) => values.Location = value),
        new(StartDate, "Start Date", "Date", false,
            ["fromdate", "assignmentstartdate", "assigneddate"],
            values => values.StartDate, (values, value) => values.StartDate = value),
        new(EndDate, "End Date", "Date", false,
            ["todate", "assignmentenddate", "returndate"],
            values => values.EndDate, (values, value) => values.EndDate = value),
        new(Purpose, "Purpose / Reason", "Text", false,
            ["purpose", "reason", "purposereason"],
            values => values.Purpose, (values, value) => values.Purpose = value),
        new(CostCenter, "Cost Center", "Lookup", false,
            ["costcentre"],
            values => values.CostCenter, (values, value) => values.CostCenter = value),
        new(Notes, "Notes", "Text", false,
            ["remarks", "notesremarks", "comments"],
            values => values.Notes, (values, value) => values.Notes = value)
    ];

    public static AssetBulkAssignmentField? Find(string? key) =>
        All.FirstOrDefault(field => string.Equals(field.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string LabelFor(string key) => Find(key)?.Label ?? key;

    /// <summary>Header text reduced to lower-case letters and digits, e.g. "Asset Tag / Serial *" → "assettagserial".</summary>
    public static string NormalizeHeader(string header) =>
        new(header.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}

/// <summary>Example rows written into the downloaded template, in AssetBulkAssignmentFields.All order.</summary>
public static class AssetBulkAssignmentSampleData
{
    public static readonly IReadOnlyList<string?[]> Rows =
    [
        ["IT-000456", "Dell Latitude 7440", "Laptop", "Employee", "Arjun Mehta", "IT Department", "Head Office - Floor 2", "2025-01-01", null, "Operational Use", "IT-001", "For daily operations"],
        ["IT-000789", "Zebra TC53", "Barcode Scanner", "Department", "Operations", "Operations", "Warehouse - WH-01", "2025-02-01", null, "Inventory Scanning", null, null],
        ["AV-000231", "Epson EB-L630U", "Projector", "Location", "Meeting Room 1", "Facilities", "Head Office - Floor 3", "2025-01-10", null, "Meeting Room Use", null, null],
        ["EQ-000112", "Toyota 8FBE16", "Forklift", "Employee", "Ramesh Kumar", "Plant Operations", "Main Plant", "2025-08-05", "2025-12-20", "Material Handling", null, null]
    ];
}

public static class AssetBulkAssignmentRowStatuses
{
    public const string Valid = "Valid";
    public const string Warning = "Warning";
    public const string Error = "Error";

    public static IEnumerable<string> All => [Valid, Warning, Error];

    public static string? Normalize(string? status) =>
        All.FirstOrDefault(value => string.Equals(value, status?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class AssetBulkAssignmentSeverities
{
    public const string Error = "Error";
    public const string Warning = "Warning";
}

/// <summary>Reads the Assignment Type column, which also accepts e.g. "Location / Site", "Site" or "Other".</summary>
public static class AssetBulkAssignmentTargets
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["employee"] = AssetAssignmentTargets.Employee,
        ["user"] = AssetAssignmentTargets.Employee,
        ["department"] = AssetAssignmentTargets.Department,
        ["location"] = AssetAssignmentTargets.Location,
        ["locationsite"] = AssetAssignmentTargets.Location,
        ["site"] = AssetAssignmentTargets.Location,
        ["otherentity"] = AssetAssignmentTargets.OtherEntity,
        ["other"] = AssetAssignmentTargets.OtherEntity,
        ["entity"] = AssetAssignmentTargets.OtherEntity
    };

    public static string? Normalize(string? target) =>
        target is not null && Aliases.TryGetValue(AssetBulkAssignmentFields.NormalizeHeader(target), out var listed) ? listed : null;
}

// ---- Requests ----

/// <summary>Step 2: Asset360 field key → file column header, e.g. { "assetTag": "Asset Tag / Serial" }. Leave a field out to skip it.</summary>
public sealed record SaveAssetBulkAssignmentMappingRequest(IReadOnlyDictionary<string, string?>? Mapping);

/// <summary>Edit Data on step 3: the new values of each changed row. Each row's values are replaced as a whole.</summary>
public sealed record UpdateAssetBulkAssignmentRowsRequest(IReadOnlyCollection<AssetBulkAssignmentRowEditRequest>? Rows);

public sealed record AssetBulkAssignmentRowEditRequest(int RowNumber, AssetBulkAssignmentValuesDto? Values);

public sealed record AssetBulkAssignmentValuesDto(
    string? AssetTag,
    string? AssetName,
    string? AssetType,
    string? AssignmentType,
    string? AssignTo,
    string? Department,
    string? Location,
    string? StartDate,
    string? EndDate,
    string? Purpose,
    string? CostCenter,
    string? Notes)
{
    public AssetBulkAssignmentValues ToEntity()
    {
        return new AssetBulkAssignmentValues
        {
            AssetTag = ContractText.TrimOrNull(AssetTag),
            AssetName = ContractText.TrimOrNull(AssetName),
            AssetType = ContractText.TrimOrNull(AssetType),
            AssignmentType = ContractText.TrimOrNull(AssignmentType),
            AssignTo = ContractText.TrimOrNull(AssignTo),
            Department = ContractText.TrimOrNull(Department),
            Location = ContractText.TrimOrNull(Location),
            StartDate = ContractText.TrimOrNull(StartDate),
            EndDate = ContractText.TrimOrNull(EndDate),
            Purpose = ContractText.TrimOrNull(Purpose),
            CostCenter = ContractText.TrimOrNull(CostCenter),
            Notes = ContractText.TrimOrNull(Notes)
        };
    }

    public static AssetBulkAssignmentValuesDto FromEntity(AssetBulkAssignmentValues values)
    {
        return new AssetBulkAssignmentValuesDto(
            values.AssetTag,
            values.AssetName,
            values.AssetType,
            values.AssignmentType,
            values.AssignTo,
            values.Department,
            values.Location,
            values.StartDate,
            values.EndDate,
            values.Purpose,
            values.CostCenter,
            values.Notes);
    }
}

/// <summary>
/// Step 4: details applied to every row that leaves them blank; values in the file win.
/// Only the section matching AssignTo is read: Employee, Department, Location or OtherEntity.
/// </summary>
/// <param name="AssignTo">One of AssetAssignmentTargets.All (the Assignment Type radio buttons).</param>
/// <param name="Department">Department and its manager (Department Manager).</param>
/// <param name="Location">Site, Building, Floor and Room / Space.</param>
/// <param name="EndDate">Leave empty when IsOpenEnded.</param>
/// <param name="AssignmentReason">e.g. "Department Use".</param>
/// <param name="OverrideExistingAssignment">Release assets from their current assignment instead of skipping them.</param>
public sealed record SaveAssetBulkAssignmentDetailsRequest(
    string? AssignTo,
    AssetAssignmentEmployeeRequest? Employee,
    AssetAssignmentDepartmentRequest? Department,
    AssetAssignmentOtherEntityRequest? OtherEntity,
    AssetAssignmentLocationRequest? Location,
    DateTime? StartDate,
    DateTime? EndDate,
    bool IsOpenEnded,
    string? AssignmentReason,
    string? Reference,
    string? Notes,
    bool OverrideExistingAssignment,
    bool NotifyAssignees,
    bool NotifyDepartmentManager,
    bool NotifyDepartment)
{
    public AssetBulkAssignmentCommonDetails ToEntity()
    {
        var assignTo = AssetAssignmentTargets.Normalize(AssignTo);
        return new AssetBulkAssignmentCommonDetails
        {
            AssignTo = assignTo,
            Employee = assignTo == AssetAssignmentTargets.Employee ? Employee?.ToEntity() : null,
            Department = Department is null || string.IsNullOrWhiteSpace(Department.DepartmentId) ? null : Department.ToEntity(),
            OtherEntity = assignTo == AssetAssignmentTargets.OtherEntity ? OtherEntity?.ToEntity() : null,
            Location = Location?.ToEntity(),
            StartDate = ContractText.DateOnly(StartDate),
            EndDate = IsOpenEnded ? null : ContractText.DateOnly(EndDate),
            IsOpenEnded = IsOpenEnded,
            AssignmentReason = ContractText.TrimOrNull(AssignmentReason),
            Reference = ContractText.TrimOrNull(Reference),
            Notes = ContractText.TrimOrNull(Notes),
            OverrideExistingAssignment = OverrideExistingAssignment,
            NotifyAssignees = NotifyAssignees,
            NotifyDepartmentManager = NotifyDepartmentManager,
            NotifyDepartment = NotifyDepartment
        };
    }
}

/// <param name="SendNotifications">"Send assignment notifications after completion" on step 5.</param>
public sealed record ConfirmAssetBulkAssignmentRequest(bool SendNotifications);

// ---- Responses ----

public sealed record AssetBulkAssignmentFieldResponse(string Key, string Label, string DataType, bool Required)
{
    public static AssetBulkAssignmentFieldResponse FromField(AssetBulkAssignmentField field) =>
        new(field.Key, field.Label, field.DataType, field.Required);
}

/// <summary>The File Information / File Summary panel.</summary>
public sealed record AssetBulkAssignmentFileInfo(
    string FileName,
    long FileSize,
    string? UploadedBy,
    DateTime? UploadedOn,
    int TotalRecords);

/// <summary>The four count cards and the Validation Summary chart. Percentages are of TotalRecords, rounded.</summary>
public sealed record AssetBulkAssignmentCounts(
    int TotalRecords,
    int ValidRecords,
    int Warnings,
    int Errors,
    int ValidPercent,
    int WarningPercent,
    int ErrorPercent)
{
    public static AssetBulkAssignmentCounts FromRows(IReadOnlyCollection<AssetBulkAssignmentRow> rows)
    {
        var valid = rows.Count(row => row.Status == AssetBulkAssignmentRowStatuses.Valid);
        var warnings = rows.Count(row => row.Status == AssetBulkAssignmentRowStatuses.Warning);
        var errors = rows.Count(row => row.Status == AssetBulkAssignmentRowStatuses.Error);

        return new AssetBulkAssignmentCounts(
            rows.Count,
            valid,
            warnings,
            errors,
            Percent(valid, rows.Count),
            Percent(warnings, rows.Count),
            Percent(errors, rows.Count));
    }

    private static int Percent(int count, int total) =>
        total == 0 ? 0 : (int)Math.Round(count * 100m / total, MidpointRounding.AwayFromZero);
}

/// <param name="CurrentStep">1 uploaded, 3 mapped and checked, 4 common details saved, 5 confirmed.</param>
/// <param name="Mapping">Saved mapping, field key → file column; empty until step 2 is saved.</param>
/// <param name="CommonDetails">Step 4; null until saved.</param>
/// <param name="Result">Step 5; null until confirmed.</param>
public sealed record AssetBulkAssignmentResponse(
    string Id,
    string BatchNumber,
    string? Status,
    int CurrentStep,
    AssetBulkAssignmentFileInfo File,
    AssetBulkAssignmentCounts Counts,
    IReadOnlyCollection<string> Columns,
    IReadOnlyDictionary<string, string> Mapping,
    AssetBulkAssignmentCommonDetails? CommonDetails,
    AssetBulkAssignmentResult? Result,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt)
{
    public static AssetBulkAssignmentResponse FromEntity(AssetBulkAssignment batch)
    {
        return new AssetBulkAssignmentResponse(
            batch.Id,
            batch.BatchNumber,
            batch.Status,
            batch.CurrentStep,
            new AssetBulkAssignmentFileInfo(batch.FileName, batch.FileSize, batch.CreatedBy, batch.CreatedAt, batch.Rows.Count),
            AssetBulkAssignmentCounts.FromRows(batch.Rows),
            batch.Columns,
            batch.Mapping.ToDictionary(map => map.Field, map => map.Column),
            batch.CommonDetails,
            batch.Result,
            batch.CreatedBy,
            batch.CreatedAt,
            batch.UpdatedBy,
            batch.UpdatedAt);
    }
}

/// <summary>One line of the File Validations panel.</summary>
public sealed record AssetBulkAssignmentFileCheck(string Name, bool Passed, string? Message);

/// <summary>Step 1 result: the stored batch, the file checks and the first rows as read.</summary>
/// <param name="PreviewRows">The first 10 rows; GET /{id}/file-rows pages through all of them (View All).</param>
/// <param name="SuggestedMapping">Field key → file column, matched by header text.</param>
public sealed record AssetBulkAssignmentUploadResponse(
    AssetBulkAssignmentResponse Batch,
    IReadOnlyCollection<AssetBulkAssignmentFileCheck> FileValidations,
    IReadOnlyCollection<AssetBulkAssignmentFileRow> PreviewRows,
    IReadOnlyDictionary<string, string> SuggestedMapping);

/// <param name="Cells">Cell text in Columns order.</param>
public sealed record AssetBulkAssignmentFileRow(int RowNumber, IReadOnlyCollection<string?> Cells);

public sealed record AssetBulkAssignmentFileRowsResponse(
    IReadOnlyCollection<string> Columns,
    IReadOnlyCollection<AssetBulkAssignmentFileRow> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>A file column on the left of the Map Columns step.</summary>
/// <param name="SampleValue">The first non-empty value in the column.</param>
/// <param name="MappedTo">The field key it is mapped to, if any.</param>
public sealed record AssetBulkAssignmentFileColumn(string ColumnName, string? SampleValue, string? MappedTo);

/// <summary>Step 2: file columns, Asset360 fields and the mapping, with the first rows as mapped.</summary>
/// <param name="Mapping">The saved mapping, or the suggested one when none is saved yet.</param>
/// <param name="SuggestedMapping">Matched by header text; Reset Mapping puts this back.</param>
/// <param name="Preview">The first 5 rows as mapped (Data Preview).</param>
public sealed record AssetBulkAssignmentMappingResponse(
    AssetBulkAssignmentResponse Batch,
    IReadOnlyCollection<AssetBulkAssignmentFileColumn> FileColumns,
    IReadOnlyCollection<AssetBulkAssignmentFieldResponse> Fields,
    IReadOnlyDictionary<string, string> Mapping,
    IReadOnlyDictionary<string, string> SuggestedMapping,
    IReadOnlyCollection<AssetBulkAssignmentMappedRow> Preview);

public sealed record AssetBulkAssignmentMappedRow(int RowNumber, AssetBulkAssignmentValuesDto Values);

/// <summary>One row of the Asset Records / Preview of Valid Records tables.</summary>
/// <remarks>
/// The display columns show what will be assigned: the file's value, or the common detail when the file leaves it blank,
/// using the names found in the system. Values holds exactly what the file (or Edit Data) gave, for editing.
/// </remarks>
/// <param name="Remarks">The first issue, for the Remarks column.</param>
/// <param name="CurrentAssignmentNumber">The active assignment the asset is already in, if any.</param>
/// <param name="AssignmentNumber">Set once confirmed.</param>
/// <param name="SkippedReason">Set when the row was skipped on confirm.</param>
public sealed record AssetBulkAssignmentRecordResponse(
    int RowNumber,
    string? Status,
    string? Remarks,
    string? AssetRegistrationId,
    string? AssetTag,
    string? AssetName,
    string? AssetType,
    string? AssignmentType,
    string? AssignTo,
    string? Department,
    string? Location,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Purpose,
    string? CostCenter,
    string? Notes,
    string? CurrentAssignmentNumber,
    bool IsEdited,
    AssetBulkAssignmentValuesDto Values,
    IReadOnlyCollection<AssetBulkAssignmentIssue> Issues,
    string? AssignmentNumber,
    string? SkippedReason)
{
    public static AssetBulkAssignmentRecordResponse FromRow(AssetBulkAssignmentRow row)
    {
        var resolved = row.Resolved;
        var values = row.Values;

        return new AssetBulkAssignmentRecordResponse(
            row.RowNumber,
            row.Status,
            row.Issues.OrderBy(issue => issue.Severity == AssetBulkAssignmentSeverities.Error ? 0 : 1).FirstOrDefault()?.Message,
            resolved?.AssetRegistrationId,
            resolved?.AssetTag ?? values.AssetTag,
            resolved?.AssetName ?? values.AssetName,
            resolved?.AssetType ?? values.AssetType,
            resolved?.AssignTo ?? values.AssignmentType,
            AssignedToName(row) ?? values.AssignTo,
            resolved?.Department?.DepartmentName ?? resolved?.Employee?.DepartmentName ?? values.Department,
            resolved?.Location?.LocationPath ?? values.Location,
            resolved?.StartDate,
            resolved?.EndDate,
            resolved?.Purpose ?? values.Purpose,
            resolved?.CostCenter ?? values.CostCenter,
            resolved?.Notes ?? values.Notes,
            resolved?.CurrentAssignmentNumber,
            row.IsEdited,
            AssetBulkAssignmentValuesDto.FromEntity(values),
            row.Issues,
            row.AssignmentNumber,
            row.SkippedReason);
    }

    /// <summary>The employee, department, place or entity name the row's asset goes to.</summary>
    public static string? AssignedToName(AssetBulkAssignmentRow row) => row.Resolved?.AssignTo switch
    {
        AssetAssignmentTargets.Employee => row.Resolved.Employee?.EmployeeName,
        AssetAssignmentTargets.Department => row.Resolved.Department?.DepartmentName,
        AssetAssignmentTargets.Location => row.Resolved.Location?.LocationPath,
        AssetAssignmentTargets.OtherEntity => row.Resolved.OtherEntityName,
        _ => null
    };
}

public sealed record AssetBulkAssignmentRecordsResponse(
    IReadOnlyCollection<AssetBulkAssignmentRecordResponse> Items,
    int TotalCount,
    AssetBulkAssignmentCounts Counts,
    int Page,
    int PageSize);

/// <summary>One line of the Errors &amp; Warnings panel.</summary>
public sealed record AssetBulkAssignmentIssueResponse(
    int RowNumber,
    string? AssetTag,
    string Field,
    string FieldLabel,
    string Severity,
    string Message,
    string? Details);

/// <param name="ErrorCount">Issue counts for the tabs, regardless of the Severity filter.</param>
public sealed record AssetBulkAssignmentIssuesResponse(
    IReadOnlyCollection<AssetBulkAssignmentIssueResponse> Items,
    int TotalCount,
    int ErrorCount,
    int WarningCount,
    int Page,
    int PageSize);

/// <summary>Step 5 result.</summary>
/// <param name="AssignedCount">Assets assigned.</param>
/// <param name="Assignments">One assignment per receiver and period; rows going to the same place together share one.</param>
public sealed record AssetBulkAssignmentConfirmResponse(
    AssetBulkAssignmentResponse Batch,
    int AssignedCount,
    int SkippedCount,
    IReadOnlyCollection<AssetBulkAssignmentCreatedAssignment> Assignments,
    IReadOnlyCollection<AssetBulkAssignmentSkippedRow> SkippedRows);

public sealed record AssetBulkAssignmentCreatedAssignment(
    string Id,
    string AssignmentNumber,
    string AssignTo,
    string? AssignedToName,
    int AssetCount);

public sealed record AssetBulkAssignmentSkippedRow(int RowNumber, string? AssetTag, string Reason);
