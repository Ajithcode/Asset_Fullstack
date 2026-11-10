using MongoDB.Bson.Serialization.Attributes;

namespace A360.Dashboard.Repository.Mobile;

// Read-only views of the records the mobile dashboard needs. Other services (Asset, Maintenance, Inspection,
// Workflows, Tracking) own and write these collections in the shared Asset360 database; the Dashboard service only
// reads the few fields below so it does not depend on those projects.

/// <summary>Asset service AssetRegistration (only the fields the mobile screens show).</summary>
[BsonIgnoreExtraElements]
public sealed class MobileAsset
{
    [BsonId] public object? Id { get; set; }
    [BsonElement("asset_registration_id")] public string AssetRegistrationId { get; set; } = string.Empty;
    [BsonElement("basic_information")] public MobileAssetBasicInformation? BasicInformation { get; set; }
    [BsonElement("classification")] public MobileAssetClassification? Classification { get; set; }
    [BsonElement("location_assignment")] public MobileAssetLocationAssignment? LocationAssignment { get; set; }
    [BsonElement("created_at")] public DateTime? CreatedAt { get; set; }
    [BsonElement("is_deleted")] public bool IsDeleted { get; set; }

    public string? AssetTag => BasicInformation?.AssetTag;
    public string? AssetName => BasicInformation?.AssetName;
    public string? Status => BasicInformation?.Status;
    public string? AssetType => Classification?.AssetType;
    public MobileAssetLocation? Location => LocationAssignment?.Location;
}

[BsonIgnoreExtraElements]
public sealed class MobileAssetBasicInformation
{
    [BsonElement("asset_name")] public string? AssetName { get; set; }
    [BsonElement("asset_tag")] public string? AssetTag { get; set; }
    [BsonElement("model")] public string? Model { get; set; }
    [BsonElement("manufacturer")] public string? Manufacturer { get; set; }
    [BsonElement("status")] public string? Status { get; set; }
    [BsonElement("condition")] public string? Condition { get; set; }
    [BsonElement("criticality")] public string? Criticality { get; set; }
    [BsonElement("images")] public List<string>? Images { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class MobileAssetClassification
{
    [BsonElement("category")] public string? Category { get; set; }
    [BsonElement("asset_type")] public string? AssetType { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class MobileAssetLocationAssignment
{
    [BsonElement("location")] public MobileAssetLocation? Location { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class MobileAssetLocation
{
    [BsonElement("site_id")] public string? SiteId { get; set; }
    [BsonElement("site_name")] public string? SiteName { get; set; }
    [BsonElement("building_name")] public string? BuildingName { get; set; }
    [BsonElement("floor_name")] public string? FloorName { get; set; }
    [BsonElement("zone_name")] public string? ZoneName { get; set; }
    [BsonElement("room_name")] public string? RoomName { get; set; }
    [BsonElement("location_path")] public string? LocationPath { get; set; }
    [BsonElement("latitude")] public double? Latitude { get; set; }
    [BsonElement("longitude")] public double? Longitude { get; set; }
}

/// <summary>Maintenance service WorkOrder; <see cref="AssetId"/> is the asset tag or registration id.</summary>
[BsonIgnoreExtraElements]
public sealed class MobileWorkOrder
{
    [BsonId] public object? Id { get; set; }
    [BsonElement("work_order_id")] public string WorkOrderId { get; set; } = string.Empty;
    [BsonElement("asset_id")] public string? AssetId { get; set; }
    [BsonElement("asset_name")] public string? AssetName { get; set; }
    /// <summary>Preventive, Corrective, Predictive, Inspection, Service.</summary>
    [BsonElement("work_type")] public string? WorkType { get; set; }
    /// <summary>Routine, Breakdown, Emergency, Improvement, Statutory.</summary>
    [BsonElement("category")] public string? Category { get; set; }
    [BsonElement("title")] public string? Title { get; set; }
    [BsonElement("priority")] public string? Priority { get; set; }
    [BsonElement("status")] public string? Status { get; set; }
    [BsonElement("scheduled_date")] public DateTime? ScheduledDate { get; set; }
    [BsonElement("due_date")] public DateTime? DueDate { get; set; }
    [BsonElement("planned_start_date")] public DateTime? PlannedStartDate { get; set; }
    [BsonElement("planned_end_date")] public DateTime? PlannedEndDate { get; set; }
    [BsonElement("actual_start_date")] public DateTime? ActualStartDate { get; set; }
    [BsonElement("actual_end_date")] public DateTime? ActualEndDate { get; set; }
    [BsonElement("assigned_technician")] public string? AssignedTechnician { get; set; }
    [BsonElement("site")] public string? Site { get; set; }
    [BsonElement("building")] public string? Building { get; set; }
    [BsonElement("area")] public string? Area { get; set; }
    [BsonElement("parts")] public List<MobileWorkOrderPart>? Parts { get; set; }
    [BsonElement("labour")] public List<MobileWorkOrderLabour>? Labour { get; set; }
    [BsonElement("additional_cost")] public double AdditionalCost { get; set; }
    [BsonElement("created_at")] public DateTime? CreatedAt { get; set; }
    [BsonElement("is_deleted")] public bool IsDeleted { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class MobileWorkOrderPart
{
    [BsonElement("quantity")] public double Quantity { get; set; }
    [BsonElement("unit_cost")] public double UnitCost { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class MobileWorkOrderLabour
{
    [BsonElement("hours")] public double Hours { get; set; }
    [BsonElement("hourly_rate")] public double HourlyRate { get; set; }
}

/// <summary>Inspection service Inspection. Its dates are stored as ISO strings.</summary>
[BsonIgnoreExtraElements]
public sealed class MobileInspection
{
    [BsonId] public object? Id { get; set; }
    [BsonElement("inspection_code")] public string InspectionCode { get; set; } = string.Empty;
    [BsonElement("title")] public string? Title { get; set; }
    [BsonElement("inspection_type_name")] public string? InspectionTypeName { get; set; }
    [BsonElement("priority")] public string? Priority { get; set; }
    [BsonElement("status")] public string? Status { get; set; }
    [BsonElement("asset_code")] public string? AssetCode { get; set; }
    [BsonElement("asset_name")] public string? AssetName { get; set; }
    [BsonElement("location")] public string? Location { get; set; }
    [BsonElement("site_name")] public string? SiteName { get; set; }
    [BsonElement("assigned_to")] public string? AssignedTo { get; set; }
    [BsonElement("due_date")] public string? DueDate { get; set; }
    [BsonElement("scheduled_start")] public string? ScheduledStart { get; set; }
    [BsonElement("scheduled_end")] public string? ScheduledEnd { get; set; }
    [BsonElement("completed_at")] public string? CompletedAt { get; set; }
    [BsonElement("findings_count")] public int FindingsCount { get; set; }
    [BsonElement("responses")] public List<MobileInspectionResponse>? Responses { get; set; }
    [BsonElement("created_at")] public DateTime? CreatedAt { get; set; }
    [BsonElement("is_deleted")] public bool IsDeleted { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class MobileInspectionResponse
{
    [BsonElement("task_title")] public string? TaskTitle { get; set; }
    [BsonElement("result")] public string? Result { get; set; }
}

/// <summary>Workflows service WorkflowApprovalTask.</summary>
[BsonIgnoreExtraElements]
public sealed class MobileApprovalTask
{
    [BsonId] public object? Id { get; set; }
    [BsonElement("workflow_name")] public string? WorkflowName { get; set; }
    [BsonElement("asset_request")] public string? AssetRequest { get; set; }
    [BsonElement("requested_by")] public string? RequestedBy { get; set; }
    [BsonElement("assigned_to")] public string? AssignedTo { get; set; }
    [BsonElement("due_date")] public DateTime? DueDate { get; set; }
    [BsonElement("priority")] public string? Priority { get; set; }
    [BsonElement("status")] public string? Status { get; set; }
    [BsonElement("created_at")] public DateTime? CreatedAt { get; set; }
    [BsonElement("is_deleted")] public bool IsDeleted { get; set; }
}

/// <summary>Tracking service IoTAlert.</summary>
[BsonIgnoreExtraElements]
public sealed class MobileIoTAlert
{
    [BsonId] public object? Id { get; set; }
    [BsonElement("alert_id")] public string AlertId { get; set; } = string.Empty;
    [BsonElement("raised_at")] public DateTime RaisedAt { get; set; }
    [BsonElement("asset_registration_id")] public string? AssetRegistrationId { get; set; }
    [BsonElement("asset_name")] public string? AssetName { get; set; }
    [BsonElement("device_id")] public string? DeviceId { get; set; }
    [BsonElement("site_id")] public string? SiteId { get; set; }
    [BsonElement("site_name")] public string? SiteName { get; set; }
    [BsonElement("event_type")] public string? EventType { get; set; }
    /// <summary>Critical, Warning or Info.</summary>
    [BsonElement("severity")] public string? Severity { get; set; }
    /// <summary>Open, Acknowledged, In Progress or Resolved.</summary>
    [BsonElement("status")] public string? Status { get; set; }
    [BsonElement("message")] public string? Message { get; set; }
    [BsonElement("is_deleted")] public bool IsDeleted { get; set; }
}

/// <summary>Maintenance service SparePart.</summary>
[BsonIgnoreExtraElements]
public sealed class MobileSparePart
{
    [BsonId] public object? Id { get; set; }
    [BsonElement("part_id")] public string PartId { get; set; } = string.Empty;
    [BsonElement("part_name")] public string? PartName { get; set; }
    [BsonElement("quantity_in_stock")] public int QuantityInStock { get; set; }
    [BsonElement("minimum_stock_level")] public int MinimumStockLevel { get; set; }
    [BsonElement("created_at")] public DateTime? CreatedAt { get; set; }
    [BsonElement("updated_at")] public DateTime? UpdatedAt { get; set; }
    [BsonElement("is_deleted")] public bool IsDeleted { get; set; }
}

/// <summary>Maintenance service PreventiveMaintenance schedule.</summary>
[BsonIgnoreExtraElements]
public sealed class MobilePmSchedule
{
    [BsonId] public object? Id { get; set; }
    [BsonElement("pm_schedule_id")] public string PmScheduleId { get; set; } = string.Empty;
    [BsonElement("schedule_name")] public string? ScheduleName { get; set; }
    [BsonElement("asset_id")] public string? AssetId { get; set; }
    [BsonElement("asset_name")] public string? AssetName { get; set; }
    [BsonElement("location")] public string? Location { get; set; }
    [BsonElement("next_due_date")] public DateTime? NextDueDate { get; set; }
    [BsonElement("priority")] public string? Priority { get; set; }
    [BsonElement("is_deleted")] public bool IsDeleted { get; set; }
}

/// <summary>The latest condition assessment of one asset.</summary>
public sealed record MobileAssetHealthScore(
    string AssetRegistrationId,
    decimal HealthScore,
    string? OverallCondition,
    string? Remarks,
    DateTime? AssessmentDate);
