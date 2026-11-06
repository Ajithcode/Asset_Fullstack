using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>One run of the Bulk Import / Export screen: an uploaded import file kept from upload until imported, or a finished export.</summary>
/// <remarks>
/// Imports go through the five steps on the screen: Download Template (1), Upload File (2), Map Fields (3), Validate (4) and Import (5).
/// Both kinds are listed in Import History. Status is BaseEntity.Status; uploaded / exported by and on are BaseEntity.CreatedBy / CreatedAt.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class AssetImportExportJob : BaseEntity
{
    // e.g. "IMP-000012" or "EXP-000004".
    [BsonElement("job_number")]
    public string JobNumber { get; set; } = string.Empty;

    // "Import" or "Export".
    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("file_name")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("file_size")]
    public long FileSize { get; set; }

    // "xlsx" or "csv".
    [BsonElement("file_format")]
    public string FileFormat { get; set; } = string.Empty;

    // Rows in the file (import) or rows written (export).
    [BsonElement("record_count")]
    public int RecordCount { get; set; }

    [BsonElement("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [BsonElement("completed_by")]
    public string? CompletedBy { get; set; }

    // Import only; null for exports.
    [BsonElement("import")]
    public AssetImportJobDetails? Import { get; set; }

    // Export only; null for imports.
    [BsonElement("export")]
    public AssetExportJobDetails? Export { get; set; }
}

public sealed class AssetImportJobDetails
{
    // "Create", "Update" or "Upsert".
    [BsonElement("import_type")]
    public string ImportType { get; set; } = string.Empty;

    // The template picked on step 1, e.g. "basic".
    [BsonElement("template")]
    public string? Template { get; set; }

    // Highest step reached, 2 to 5.
    [BsonElement("current_step")]
    public int CurrentStep { get; set; }

    // Header text of each file column, in file order. Row cells are stored in the same order.
    [BsonElement("columns")]
    public List<string> Columns { get; set; } = [];

    // Blank rows between data rows; they are skipped.
    [BsonElement("empty_row_count")]
    public int EmptyRowCount { get; set; }

    // File column → Asset360 field key. Unmapped columns are left out.
    [BsonElement("mapping")]
    public List<AssetImportColumnMap> Mapping { get; set; } = [];

    [BsonElement("rows")]
    public List<AssetImportJobRow> Rows { get; set; } = [];

    // Set by Validate and again by Import.
    [BsonElement("summary")]
    public AssetImportJobSummary? Summary { get; set; }
}

public sealed class AssetImportColumnMap
{
    [BsonElement("column")]
    public string Column { get; set; } = string.Empty;

    // An import field key, e.g. "assetTag".
    [BsonElement("field")]
    public string Field { get; set; } = string.Empty;
}

public sealed class AssetImportJobRow
{
    // The row number in the file, so users can find it.
    [BsonElement("row_number")]
    public int RowNumber { get; set; }

    // Cell text in Columns order, as read from the file.
    [BsonElement("cells")]
    public List<string?> Cells { get; set; } = [];

    // "Valid", "Invalid" or "Duplicate"; null until validated. After import: "Imported" or "Failed".
    [BsonElement("status")]
    public string? Status { get; set; }

    // "Create" or "Update": what importing the row does.
    [BsonElement("action")]
    public string? Action { get; set; }

    // The asset the row updates, or the one it created.
    [BsonElement("asset_registration_id")]
    public string? AssetRegistrationId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("asset_tag")]
    public string? AssetTag { get; set; }

    [BsonElement("issues")]
    public List<AssetImportJobIssue> Issues { get; set; } = [];
}

public sealed class AssetImportJobIssue
{
    // The import field key, e.g. "purchaseDate"; null for issues about the whole row.
    [BsonElement("field")]
    public string? Field { get; set; }

    // The file column the value came from.
    [BsonElement("column")]
    public string? Column { get; set; }

    [BsonElement("message")]
    public string Message { get; set; } = string.Empty;
}

public sealed class AssetImportJobSummary
{
    [BsonElement("total")]
    public int Total { get; set; }

    [BsonElement("valid")]
    public int Valid { get; set; }

    [BsonElement("invalid")]
    public int Invalid { get; set; }

    [BsonElement("duplicate")]
    public int Duplicate { get; set; }

    // Of the valid rows, how many create a new asset and how many update one.
    [BsonElement("to_create")]
    public int ToCreate { get; set; }

    [BsonElement("to_update")]
    public int ToUpdate { get; set; }

    // Set by Import.
    [BsonElement("created")]
    public int Created { get; set; }

    [BsonElement("updated")]
    public int Updated { get; set; }

    [BsonElement("failed")]
    public int Failed { get; set; }

    [BsonElement("validated_at")]
    public DateTime? ValidatedAt { get; set; }
}

public sealed class AssetExportJobDetails
{
    // "AssetMasterData", "AssetWithRelatedData" or "CustomExport".
    [BsonElement("export_type")]
    public string ExportType { get; set; } = string.Empty;

    // "Standard", "All" or "Custom".
    [BsonElement("field_set")]
    public string FieldSet { get; set; } = string.Empty;

    // The field keys written, in column order.
    [BsonElement("fields")]
    public List<string> Fields { get; set; } = [];

    [BsonElement("filters")]
    public AssetExportJobFilters Filters { get; set; } = new();
}

public sealed class AssetExportJobFilters
{
    [BsonElement("search")]
    public string? Search { get; set; }

    [BsonElement("category")]
    public string? Category { get; set; }

    [BsonElement("asset_type")]
    public string? AssetType { get; set; }

    [BsonElement("status")]
    public string? Status { get; set; }

    [BsonElement("location_id")]
    public string? LocationId { get; set; }

    [BsonElement("department")]
    public string? Department { get; set; }

    [BsonElement("criticality")]
    public string? Criticality { get; set; }

    [BsonElement("condition")]
    public string? Condition { get; set; }

    [BsonElement("manufacturer")]
    public string? Manufacturer { get; set; }

    [BsonElement("next_maintenance_from")]
    public DateTime? NextMaintenanceFrom { get; set; }

    [BsonElement("next_maintenance_to")]
    public DateTime? NextMaintenanceTo { get; set; }
}
