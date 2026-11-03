using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Asset.Domain.Entities;

/// <summary>One or more assets assigned to an employee, department, location/site or other entity, from the Assign Asset wizard.</summary>
/// <remarks>
/// Holds all four steps: asset selection, assignment details, additional information and the confirmed result.
/// Status (Draft/Active) is BaseEntity.Status; assigned by / on are BaseEntity.CreatedBy / CreatedAt.
/// Asset, employee and department names are copied when saved, so the record can be shown without loading them.
/// </remarks>
[BsonIgnoreExtraElements]
public sealed class AssetAssignmentRecord : BaseEntity
{
    // e.g. "ASG-000012".
    [BsonElement("assignment_number")]
    public string AssignmentNumber { get; set; } = string.Empty;

    // Step 1: "Single", "Multiple" or "Scan"; "Bulk Upload" when made by the Bulk Assignment wizard.
    [BsonElement("selection_mode")]
    public string? SelectionMode { get; set; }

    // The AssetBulkAssignment this was made from, if any.
    [BsonElement("bulk_assignment_id")]
    public string? BulkAssignmentId { get; set; }

    [BsonElement("assets")]
    public List<AssetAssignmentAsset> Assets { get; set; } = [];

    // Step 2: "Employee", "Department", "Location" or "Other Entity".
    [BsonElement("assign_to")]
    public string AssignTo { get; set; } = string.Empty;

    // e.g. "Primary User", "Department Use", "Primary Location", "Temporary Transfer".
    [BsonElement("assignment_type")]
    public string? AssignmentType { get; set; }

    [BsonElement("from_date")]
    public DateTime? FromDate { get; set; }

    // Null when open ended.
    [BsonElement("to_date")]
    public DateTime? ToDate { get; set; }

    [BsonElement("is_open_ended")]
    public bool IsOpenEnded { get; set; }

    // Only the section matching AssignTo is set.
    [BsonElement("employee")]
    public AssetAssignmentEmployee? Employee { get; set; }

    [BsonElement("department")]
    public AssetAssignmentDepartment? Department { get; set; }

    [BsonElement("other_entity")]
    public AssetAssignmentOtherEntity? OtherEntity { get; set; }

    // Where the assets will be kept: the chosen site for "Location", the condition fields otherwise.
    [BsonElement("location")]
    public AssetAssignmentLocation? Location { get; set; }

    [BsonElement("cost_center")]
    public string? CostCenter { get; set; }

    [BsonElement("purpose")]
    public string? Purpose { get; set; }

    [BsonElement("approval_reference")]
    public string? ApprovalReference { get; set; }

    // Assignment Notes on step 2.
    [BsonElement("remarks")]
    public string? Remarks { get; set; }

    // Step 3: Additional Information.
    [BsonElement("notes")]
    public string? Notes { get; set; }

    [BsonElement("attachments")]
    public List<AssetAssignmentAttachment> Attachments { get; set; } = [];

    [BsonElement("notification")]
    public AssetAssignmentNotification Notification { get; set; } = new();

    [BsonElement("reminder")]
    public AssetAssignmentReminder Reminder { get; set; } = new();

    [BsonElement("custom_fields")]
    public AssetAssignmentCustomFields CustomFields { get; set; } = new();
}

/// <summary>A selected asset, as it was when assigned.</summary>
public sealed class AssetAssignmentAsset
{
    [BsonElement("asset_registration_id")]
    public string AssetRegistrationId { get; set; } = string.Empty;

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("asset_tag")]
    public string? AssetTag { get; set; }

    [BsonElement("serial_number")]
    public string? SerialNumber { get; set; }

    [BsonElement("model")]
    public string? Model { get; set; }

    [BsonElement("manufacturer")]
    public string? Manufacturer { get; set; }

    [BsonElement("category")]
    public string? Category { get; set; }

    [BsonElement("asset_type")]
    public string? AssetType { get; set; }

    [BsonElement("current_location")]
    public string? CurrentLocation { get; set; }

    // The asset's status before this assignment, e.g. "Available".
    [BsonElement("previous_status")]
    public string? PreviousStatus { get; set; }
}

public sealed class AssetAssignmentEmployee
{
    [BsonElement("employee_id")]
    public string EmployeeId { get; set; } = string.Empty;

    // e.g. "EMP-00125".
    [BsonElement("employee_code")]
    public string? EmployeeCode { get; set; }

    [BsonElement("employee_name")]
    public string? EmployeeName { get; set; }

    [BsonElement("designation")]
    public string? Designation { get; set; }

    [BsonElement("department_id")]
    public string? DepartmentId { get; set; }

    [BsonElement("department_name")]
    public string? DepartmentName { get; set; }

    [BsonElement("manager")]
    public AssetAssignmentPerson? Manager { get; set; }
}

public sealed class AssetAssignmentDepartment
{
    [BsonElement("department_id")]
    public string DepartmentId { get; set; } = string.Empty;

    [BsonElement("department_name")]
    public string? DepartmentName { get; set; }

    [BsonElement("manager")]
    public AssetAssignmentPerson? Manager { get; set; }
}

/// <summary>A manager or responsible person picked from a user list.</summary>
public sealed class AssetAssignmentPerson
{
    [BsonElement("id")]
    public string? Id { get; set; }

    [BsonElement("code")]
    public string? Code { get; set; }

    [BsonElement("name")]
    public string? Name { get; set; }

    [BsonElement("designation")]
    public string? Designation { get; set; }
}

/// <summary>Vendor, contractor, customer, partner, project, cost center or other party receiving the assets.</summary>
public sealed class AssetAssignmentOtherEntity
{
    // "Vendor / Supplier", "Contractor", "Customer / Client", "Partner", "Project", "Cost Center" or "Other".
    [BsonElement("entity_type")]
    public string EntityType { get; set; } = string.Empty;

    [BsonElement("entity_id")]
    public string? EntityId { get; set; }

    [BsonElement("entity_name")]
    public string EntityName { get; set; } = string.Empty;

    [BsonElement("contact_person")]
    public string? ContactPerson { get; set; }

    [BsonElement("contact_phone")]
    public string? ContactPhone { get; set; }

    [BsonElement("contact_email")]
    public string? ContactEmail { get; set; }

    // Agreement / PO reference, e.g. "PO-2025-0187".
    [BsonElement("agreement_reference")]
    public string? AgreementReference { get; set; }

    // e.g. "Repair / Service".
    [BsonElement("reason")]
    public string? Reason { get; set; }

    // Service / Work Details.
    [BsonElement("service_type")]
    public string? ServiceType { get; set; }

    [BsonElement("service_request_number")]
    public string? ServiceRequestNumber { get; set; }

    [BsonElement("service_location")]
    public string? ServiceLocation { get; set; }

    [BsonElement("expected_return_date")]
    public DateTime? ExpectedReturnDate { get; set; }

    // e.g. "Same as Assigned".
    [BsonElement("return_condition")]
    public string? ReturnCondition { get; set; }

    [BsonElement("responsible_person")]
    public AssetAssignmentPerson? ResponsiblePerson { get; set; }
}

public sealed class AssetAssignmentLocation
{
    [BsonElement("site_id")]
    public string? SiteId { get; set; }

    [BsonElement("site_code")]
    public string? SiteCode { get; set; }

    [BsonElement("site_name")]
    public string? SiteName { get; set; }

    [BsonElement("building_id")]
    public string? BuildingId { get; set; }

    [BsonElement("building_name")]
    public string? BuildingName { get; set; }

    [BsonElement("floor_id")]
    public string? FloorId { get; set; }

    [BsonElement("floor_name")]
    public string? FloorName { get; set; }

    [BsonElement("room_id")]
    public string? RoomId { get; set; }

    [BsonElement("room_name")]
    public string? RoomName { get; set; }

    // Built on the server from the level names, e.g. "Head Office > Block A > Floor 2 > IT-204".
    [BsonElement("location_path")]
    public string? LocationPath { get; set; }
}

/// <summary>A file uploaded through the Media service; this keeps its link and details.</summary>
public sealed class AssetAssignmentAttachment
{
    [BsonElement("file_name")]
    public string FileName { get; set; } = string.Empty;

    [BsonElement("file_url")]
    public string FileUrl { get; set; } = string.Empty;

    [BsonElement("content_type")]
    public string? ContentType { get; set; }

    [BsonElement("file_size")]
    public long? FileSize { get; set; }
}

public sealed class AssetAssignmentNotification
{
    [BsonElement("notify_assignees")]
    public bool NotifyAssignees { get; set; }

    [BsonElement("message")]
    public string? Message { get; set; }

    [BsonElement("notify_manager")]
    public bool NotifyManager { get; set; }

    [BsonElement("notify_department")]
    public bool NotifyDepartment { get; set; }
}

public sealed class AssetAssignmentReminder
{
    [BsonElement("is_enabled")]
    public bool IsEnabled { get; set; }

    [BsonElement("reminder_date")]
    public DateTime? ReminderDate { get; set; }

    [BsonElement("message")]
    public string? Message { get; set; }
}

public sealed class AssetAssignmentCustomFields
{
    // e.g. "IT-ASSIGN-2025-006".
    [BsonElement("assignment_reference")]
    public string? AssignmentReference { get; set; }

    [BsonElement("requestor_id")]
    public string? RequestorId { get; set; }

    [BsonElement("requestor_name")]
    public string? RequestorName { get; set; }

    // e.g. "Normal".
    [BsonElement("priority")]
    public string? Priority { get; set; }

    [BsonElement("assignment_reason")]
    public string? AssignmentReason { get; set; }
}
