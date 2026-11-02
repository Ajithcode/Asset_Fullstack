using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Maintenance.Domain.Entities;

[BsonIgnoreExtraElements]
public class PreventiveMaintenance : BaseEntity
{
    [BsonElement("pm_schedule_id")]
    public string PmScheduleId { get; set; } = null!;

    [BsonElement("schedule_name")]
    public string? ScheduleName { get; set; }

    [BsonElement("asset_id")]
    public string AssetId { get; set; } = null!;

    [BsonElement("asset_name")]
    public string AssetName { get; set; } = null!;

    [BsonElement("asset_type")]
    public string? AssetType { get; set; }

    [BsonElement("location")]
    public string? Location { get; set; }

    /// <summary>Monthly, Quarterly, Bi-Annual, Yearly, Every 250 Hours, ...</summary>
    [BsonElement("frequency")]
    public string Frequency { get; set; } = null!;

    /// <summary>Maintenance type: Time Based, Usage Based, Condition Based, Calendar Based.</summary>
    [BsonElement("trigger_type")]
    public string TriggerType { get; set; } = null!;

    [BsonElement("last_maintenance_date")]
    public DateTime? LastMaintenanceDate { get; set; }

    [BsonElement("next_due_date")]
    public DateTime? NextDueDate { get; set; }

    [BsonElement("auto_create_work_order")]
    public bool AutoCreateWorkOrder { get; set; }

    [BsonElement("priority")]
    public string? Priority { get; set; }

    [BsonElement("assigned_to")]
    public string? AssignedTo { get; set; }

    [BsonElement("estimated_hours")]
    public double? EstimatedHours { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    /// <summary>Checklist steps copied onto each generated work order.</summary>
    [BsonElement("checklist")]
    public List<string> Checklist { get; set; } = [];
}
