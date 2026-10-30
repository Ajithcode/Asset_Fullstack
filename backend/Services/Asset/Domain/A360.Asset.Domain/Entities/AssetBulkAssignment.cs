using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>One uploaded file of the Bulk Assignment wizard, kept from upload until it is confirmed.</summary>
/// <remarks>
/// Holds all five steps: the file as read (step 1), the column mapping (step 2), each row with its checks (step 3),
/// the common details (step 4) and the outcome of confirming (step 5).
/// Status (Draft/Completed) is BaseEntity.Status; uploaded by / on are BaseEntity.CreatedBy / CreatedAt.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class AssetBulkAssignment : BaseEntity
{
    // e.g. "BAS-000012".
    [BsonElement("batch_number")]
    public string BatchNumber { get; set; } = string.Empty;

    // Highest step saved so far, 1 to 5.
    [BsonElement("current_step")]
    public int CurrentStep { get; set; }

    [BsonElement("file_name")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("file_size")]
    public long FileSize { get; set; }

    // Header text of each file column, in file order. Row values are stored in the same order.
    [BsonElement("columns")]
    public List<string> Columns { get; set; } = [];

    // Blank rows between data rows; they are skipped.
    [BsonElement("empty_row_count")]
    public int EmptyRowCount { get; set; }

    // Step 2: Asset360 field key → file column header. Unmapped fields are left out.
    [BsonElement("mapping")]
    public List<AssetBulkAssignmentColumnMap> Mapping { get; set; } = [];

    [BsonElement("rows")]
    public List<AssetBulkAssignmentRow> Rows { get; set; } = [];

    // Step 4; null until saved.
    [BsonElement("common_details")]
    public AssetBulkAssignmentCommonDetails? CommonDetails { get; set; }

    // Step 5; null until confirmed.
    [BsonElement("result")]
    public AssetBulkAssignmentResult? Result { get; set; }
}

public sealed class AssetBulkAssignmentColumnMap
{
    // One of the bulk assignment field keys, e.g. "assetTag".
    [BsonElement("field")]
    public string Field { get; set; } = string.Empty;

    [BsonElement("column")]
    public string Column { get; set; } = string.Empty;
}

/// <summary>One data row of the file, its mapped values and the outcome of checking it.</summary>
public sealed class AssetBulkAssignmentRow
{
    // The row number in the file, so users can find it.
    [BsonElement("row_number")]
    public int RowNumber { get; set; }

    // Cell text in Columns order, as read from the file.
    [BsonElement("cells")]
    public List<string?> Cells { get; set; } = [];

    // Values taken from the mapped columns; Edit Data on step 3 changes these, not Cells.
    [BsonElement("values")]
    public AssetBulkAssignmentValues Values { get; set; } = new();

    [BsonElement("is_edited")]
    public bool IsEdited { get; set; }

    // "Valid", "Warning" or "Error"; null until the row is checked.
    [BsonElement("status")]
    public string? Status { get; set; }

    [BsonElement("issues")]
    public List<AssetBulkAssignmentIssue> Issues { get; set; } = [];

    // What the row resolved to, after the common details fill the blanks.
    [BsonElement("resolved")]
    public AssetBulkAssignmentResolved? Resolved { get; set; }

    // Set when the row was assigned on confirm.
    [BsonElement("assignment_id")]
    public string? AssignmentId { get; set; }

    [BsonElement("assignment_number")]
    public string? AssignmentNumber { get; set; }

    // Set when the row was skipped on confirm.
    [BsonElement("skipped_reason")]
    public string? SkippedReason { get; set; }
}

/// <summary>A row's values for the Asset360 fields, as text from the file.</summary>
public sealed class AssetBulkAssignmentValues
{
    // Asset tag or serial number.
    [BsonElement("asset_tag")]
    public string? AssetTag { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("asset_type")]
    public string? AssetType { get; set; }

    // "Employee", "Department", "Location" or "Other Entity".
    [BsonElement("assignment_type")]
    public string? AssignmentType { get; set; }

    // Employee name or code, department, location or entity name, depending on AssignmentType.
    [BsonElement("assign_to")]
    public string? AssignTo { get; set; }

    [BsonElement("department")]
    public string? Department { get; set; }

    // e.g. "Head Office - Floor 2".
    [BsonElement("location")]
    public string? Location { get; set; }

    [BsonElement("start_date")]
    public string? StartDate { get; set; }

    [BsonElement("end_date")]
    public string? EndDate { get; set; }

    [BsonElement("purpose")]
    public string? Purpose { get; set; }

    [BsonElement("cost_center")]
    public string? CostCenter { get; set; }

    [BsonElement("notes")]
    public string? Notes { get; set; }
}

public sealed class AssetBulkAssignmentIssue
{
    // The Asset360 field key the issue is about, e.g. "startDate".
    [BsonElement("field")]
    public string Field { get; set; } = string.Empty;

    // "Error" or "Warning".
    [BsonElement("severity")]
    public string Severity { get; set; } = string.Empty;

    // Short text for the table, e.g. "Asset not found".
    [BsonElement("message")]
    public string Message { get; set; } = string.Empty;

    // Longer text for the Details column, e.g. "Asset tag does not exist in the system."
    [BsonElement("details")]
    public string? Details { get; set; }
}

/// <summary>The asset and receiver a checked row points at, with names copied for display.</summary>
public sealed class AssetBulkAssignmentResolved
{
    [BsonElement("asset_registration_id")]
    public string? AssetRegistrationId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("asset_tag")]
    public string? AssetTag { get; set; }

    [BsonElement("asset_type")]
    public string? AssetType { get; set; }

    [BsonElement("assign_to")]
    public string? AssignTo { get; set; }

    [BsonElement("employee")]
    public AssetAssignmentEmployee? Employee { get; set; }

    [BsonElement("department")]
    public AssetAssignmentDepartment? Department { get; set; }

    [BsonElement("other_entity_name")]
    public string? OtherEntityName { get; set; }

    [BsonElement("location")]
    public AssetAssignmentLocation? Location { get; set; }

    [BsonElement("start_date")]
    public DateTime? StartDate { get; set; }

    [BsonElement("end_date")]
    public DateTime? EndDate { get; set; }

    [BsonElement("purpose")]
    public string? Purpose { get; set; }

    [BsonElement("cost_center")]
    public string? CostCenter { get; set; }

    [BsonElement("notes")]
    public string? Notes { get; set; }

    // The active assignment the asset is already in, e.g. "ASG-000012".
    [BsonElement("current_assignment_number")]
    public string? CurrentAssignmentNumber { get; set; }
}

/// <summary>Step 4: values applied to every row that leaves them blank.</summary>
public sealed class AssetBulkAssignmentCommonDetails
{
    // "Employee", "Department", "Location" or "Other Entity".
    [BsonElement("assign_to")]
    public string? AssignTo { get; set; }

    [BsonElement("employee")]
    public AssetAssignmentEmployee? Employee { get; set; }

    [BsonElement("department")]
    public AssetAssignmentDepartment? Department { get; set; }

    [BsonElement("other_entity")]
    public AssetAssignmentOtherEntity? OtherEntity { get; set; }

    [BsonElement("location")]
    public AssetAssignmentLocation? Location { get; set; }

    [BsonElement("start_date")]
    public DateTime? StartDate { get; set; }

    // Null when open ended.
    [BsonElement("end_date")]
    public DateTime? EndDate { get; set; }

    [BsonElement("is_open_ended")]
    public bool IsOpenEnded { get; set; }

    // e.g. "Department Use".
    [BsonElement("assignment_reason")]
    public string? AssignmentReason { get; set; }

    // e.g. "IT-ASSIGN-2025-006".
    [BsonElement("reference")]
    public string? Reference { get; set; }

    [BsonElement("notes")]
    public string? Notes { get; set; }

    // Release assets from their current assignment instead of skipping them.
    [BsonElement("override_existing_assignment")]
    public bool OverrideExistingAssignment { get; set; }

    [BsonElement("notify_assignees")]
    public bool NotifyAssignees { get; set; }

    [BsonElement("notify_department_manager")]
    public bool NotifyDepartmentManager { get; set; }

    [BsonElement("notify_department")]
    public bool NotifyDepartment { get; set; }
}

public sealed class AssetBulkAssignmentResult
{
    [BsonElement("completed_at")]
    public DateTime CompletedAt { get; set; }

    [BsonElement("completed_by")]
    public string? CompletedBy { get; set; }

    [BsonElement("send_notifications")]
    public bool SendNotifications { get; set; }

    [BsonElement("assigned_count")]
    public int AssignedCount { get; set; }

    [BsonElement("skipped_count")]
    public int SkippedCount { get; set; }

    [BsonElement("assignment_ids")]
    public List<string> AssignmentIds { get; set; } = [];
}
