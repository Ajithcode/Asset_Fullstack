using A360.Asset.Domain.Entities;

namespace A360.Asset.Api.Contracts;

/// <summary>Templates in the Select Template dropdown.</summary>
public static class AssetImportTemplates
{
    public const string Basic = "basic";
    public const string Full = "full";
    public const string Update = "update";

    public static readonly IReadOnlyList<AssetImportTemplateResponse> All =
    [
        new(Basic, "Asset Master Template (Basic)", "Mandatory fields and the most used ones, for creating assets", AssetImportTypes.Create),
        new(Full, "Asset Master Template (Full)", "Every field that can be imported", AssetImportTypes.Create),
        new(Update, "Asset Update Template", "Asset ID and Asset Tag plus every field that can be changed; blank cells keep the current value", AssetImportTypes.Update)
    ];

    public static AssetImportTemplateResponse? Find(string? key) =>
        All.FirstOrDefault(template => string.Equals(template.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Columns of a template, in order.</summary>
    public static IReadOnlyList<AssetImportField> Fields(string key) => key switch
    {
        Full => AssetImportExportFields.Importable.Where(field => field.Key != AssetImportFields.AssetId).ToList(),
        Update => AssetImportExportFields.Importable.ToList(),
        _ => AssetImportExportFields.Importable
            .Where(field => field.Key != AssetImportFields.AssetId && (field.RequiredForCreate || field.Standard))
            .ToList()
    };
}

/// <summary>Export types, field sets and file formats of the Export Assets panel.</summary>
public static class AssetExportOptions
{
    public const string AssetMasterData = "AssetMasterData";
    public const string AssetWithRelatedData = "AssetWithRelatedData";
    public const string CustomExport = "CustomExport";

    public const string StandardFields = "Standard";
    public const string AllFields = "All";
    public const string CustomFields = "Custom";

    public const string Xlsx = "xlsx";
    public const string Csv = "csv";

    // A larger export would be slow to build in memory; narrow the filters instead.
    public const int MaxRows = 20000;

    public static readonly OptionList ExportTypes = new(AssetMasterData, AssetWithRelatedData, CustomExport);
    public static readonly OptionList FieldSets = new(StandardFields, AllFields, CustomFields);
    public static readonly OptionList FileFormats = new(Xlsx, Csv);
}

public sealed record AssetImportTemplateResponse(string Key, string Name, string Description, string ImportType);

public sealed record AssetImportExportOptionResponse(string Value, string Label);

public sealed record AssetImportExportOptionsResponse(
    IReadOnlyCollection<AssetImportExportOptionResponse> ImportTypes,
    IReadOnlyCollection<AssetImportTemplateResponse> Templates,
    IReadOnlyCollection<AssetImportExportOptionResponse> ExportTypes,
    IReadOnlyCollection<AssetImportExportOptionResponse> FieldSets,
    IReadOnlyCollection<AssetImportExportOptionResponse> FileFormats,
    int MaxImportRows,
    int MaxExportRows,
    long MaxFileSizeBytes)
{
    public static readonly AssetImportExportOptionsResponse Instance = new(
        [
            new(AssetImportTypes.Create, "Create New Assets"),
            new(AssetImportTypes.Update, "Update Existing Assets"),
            new(AssetImportTypes.Upsert, "Create or Update (Upsert)")
        ],
        AssetImportTemplates.All,
        [
            new(AssetExportOptions.AssetMasterData, "Asset Master Data"),
            new(AssetExportOptions.AssetWithRelatedData, "Asset with Related Data"),
            new(AssetExportOptions.CustomExport, "Custom Export")
        ],
        [
            new(AssetExportOptions.StandardFields, "Standard Fields (Recommended)"),
            new(AssetExportOptions.AllFields, "All Fields"),
            new(AssetExportOptions.CustomFields, "Custom Fields")
        ],
        [
            new(AssetExportOptions.Xlsx, "Microsoft Excel (.xlsx)"),
            new(AssetExportOptions.Csv, "CSV (.csv)")
        ],
        AssetImportExportFields.MaxRows,
        AssetExportOptions.MaxRows,
        AssetImportExportFields.MaxFileSizeBytes);
}

/// <param name="Mandatory">Must be mapped for the import type asked for (Create when none is given).</param>
public sealed record AssetImportFieldResponse(
    string Key,
    string Label,
    string Group,
    string DataType,
    bool Mandatory,
    bool Importable,
    bool Standard,
    string? Hint,
    IReadOnlyCollection<string>? Options)
{
    public static AssetImportFieldResponse FromField(AssetImportField field, string importType) => new(
        field.Key,
        field.Label,
        field.Group,
        field.DataType,
        field.IsMandatory(importType),
        field.Importable,
        field.Standard,
        field.Hint,
        field.Options?.All);
}

public sealed record AssetImportJobSummaryResponse(
    int Total,
    int Valid,
    int Invalid,
    int Duplicate,
    int ToCreate,
    int ToUpdate,
    int Created,
    int Updated,
    int Failed,
    DateTime? ValidatedAt)
{
    public static AssetImportJobSummaryResponse? FromEntity(AssetImportJobSummary? summary) => summary is null
        ? null
        : new(summary.Total, summary.Valid, summary.Invalid, summary.Duplicate, summary.ToCreate, summary.ToUpdate,
            summary.Created, summary.Updated, summary.Failed, summary.ValidatedAt);
}

/// <summary>An import as the screen shows it: which step it is on and its counts.</summary>
public sealed record AssetImportJobResponse(
    string Id,
    string JobNumber,
    string FileName,
    long FileSize,
    string FileFormat,
    string ImportType,
    string? Template,
    int CurrentStep,
    string? Status,
    int TotalRecords,
    int ColumnCount,
    int EmptyRowCount,
    AssetImportJobSummaryResponse? Summary,
    string? CreatedBy,
    DateTime? CreatedAt,
    DateTime? CompletedAt)
{
    public static AssetImportJobResponse FromEntity(AssetImportExportJob job)
    {
        var import = job.Import ?? new AssetImportJobDetails();
        return new(
            job.Id,
            job.JobNumber,
            job.FileName,
            job.FileSize,
            job.FileFormat,
            import.ImportType,
            import.Template,
            import.CurrentStep,
            job.Status,
            job.RecordCount,
            import.Columns.Count,
            import.EmptyRowCount,
            AssetImportJobSummaryResponse.FromEntity(import.Summary),
            job.CreatedBy,
            job.CreatedAt,
            job.CompletedAt);
    }
}

public sealed record AssetImportFileCheck(string Name, bool Passed, string? Message);

/// <param name="Column">The file column (the left side of the Field Mapping table).</param>
/// <param name="MappedTo">The Asset360 field key, or null when the column is not imported.</param>
/// <param name="Status">"Mapped" or "Not Mapped".</param>
public sealed record AssetImportColumnMappingResponse(
    string Column,
    string? MappedTo,
    string? MappedToLabel,
    string? DataType,
    bool Mandatory,
    string? SampleData,
    string Status);

/// <param name="MissingMandatory">Labels of the mandatory fields no column is mapped to yet ("still needed").</param>
public sealed record AssetImportMappingResponse(
    AssetImportJobResponse Import,
    IReadOnlyCollection<AssetImportColumnMappingResponse> Columns,
    int MappedCount,
    IReadOnlyCollection<string> MissingMandatory,
    bool CanValidate,
    IReadOnlyDictionary<string, string> SuggestedMapping,
    IReadOnlyCollection<AssetImportFieldResponse> Fields);

public sealed record AssetImportUploadResponse(
    AssetImportJobResponse Import,
    IReadOnlyCollection<AssetImportFileCheck> FileChecks,
    AssetImportMappingResponse Mapping);

/// <param name="ImportType">Changes the import type picked on upload; omit to keep it.</param>
/// <param name="Mapping">File column → Asset360 field key. A null or empty key leaves the column unmapped.</param>
public sealed record SaveAssetImportMappingRequest(string? ImportType, Dictionary<string, string?>? Mapping);

public sealed record AssetImportIssueResponse(string? Field, string? Column, string Message);

/// <param name="Values">Mapped values as read from the file, keyed by Asset360 field key.</param>
public sealed record AssetImportRecordResponse(
    int RowNumber,
    string? Status,
    string? Action,
    string? AssetRegistrationId,
    string? AssetName,
    string? AssetTag,
    IReadOnlyDictionary<string, string?> Values,
    IReadOnlyCollection<AssetImportIssueResponse> Issues);

public sealed record AssetImportRecordsResponse(
    IReadOnlyCollection<AssetImportRecordResponse> Items,
    long TotalCount,
    int Page,
    int PageSize);

/// <param name="Preview">The first valid records (Import Preview).</param>
public sealed record AssetImportValidationResponse(
    AssetImportJobResponse Import,
    AssetImportJobSummaryResponse Summary,
    IReadOnlyCollection<AssetImportRecordResponse> Preview,
    IReadOnlyCollection<AssetImportRecordResponse> Problems);

public sealed record AssetImportConfirmResponse(
    AssetImportJobResponse Import,
    AssetImportJobSummaryResponse Summary,
    IReadOnlyCollection<AssetImportRecordResponse> Failed);

/// <summary>The Export Assets panel.</summary>
/// <param name="ExportType">AssetMasterData (default), AssetWithRelatedData (adds Components, Assignments, Specifications and Custom Fields sheets; .xlsx only) or CustomExport.</param>
/// <param name="FieldSet">Standard (default) or All; ignored when Fields is given.</param>
/// <param name="Fields">Field keys to export, in column order; required for CustomExport.</param>
/// <param name="FileFormat">xlsx (default) or csv.</param>
public sealed record AssetExportRequest(
    string? ExportType,
    string? FieldSet,
    List<string>? Fields,
    AssetExportFilters? Filters,
    string? FileFormat);

/// <param name="LocationId">Any level of the location hierarchy.</param>
/// <param name="NextMaintenanceFrom">Date Range (next maintenance), inclusive.</param>
public sealed record AssetExportFilters(
    string? Search,
    string? Category,
    string? AssetType,
    string? Status,
    string? LocationId,
    string? Department,
    string? Criticality,
    string? Condition,
    string? Manufacturer,
    DateTime? NextMaintenanceFrom,
    DateTime? NextMaintenanceTo);

public sealed record AssetExportCountResponse(long Count, int MaxRows);

/// <summary>One row of Import History.</summary>
/// <param name="Records">Rows in the file (import) or rows written (export).</param>
public sealed record AssetImportExportHistoryItemResponse(
    string Id,
    string JobNumber,
    string FileName,
    string Type,
    int Records,
    string? Status,
    DateTime? CreatedAt,
    string? CreatedBy,
    DateTime? CompletedAt,
    string? ImportType,
    string? ExportType,
    AssetImportJobSummaryResponse? Summary)
{
    public static AssetImportExportHistoryItemResponse FromEntity(AssetImportExportJob job) => new(
        job.Id,
        job.JobNumber,
        job.FileName,
        job.Type,
        job.RecordCount,
        job.Status,
        job.CreatedAt,
        job.CreatedBy,
        job.CompletedAt,
        job.Import?.ImportType,
        job.Export?.ExportType,
        AssetImportJobSummaryResponse.FromEntity(job.Import?.Summary));
}

public sealed record AssetImportExportHistoryResponse(
    IReadOnlyCollection<AssetImportExportHistoryItemResponse> Items,
    long TotalCount,
    int Page,
    int PageSize);

/// <summary>Row statuses of an import.</summary>
public static class AssetImportRowStatuses
{
    public const string Valid = "Valid";
    public const string Invalid = "Invalid";
    public const string Duplicate = "Duplicate";
    public const string Imported = "Imported";
    public const string Failed = "Failed";

    private static readonly OptionList List = new(Valid, Invalid, Duplicate, Imported, Failed);

    public static string? Normalize(string? value) => List.Normalize(value);
}

public static class AssetImportRowActions
{
    public const string Create = "Create";
    public const string Update = "Update";
}
