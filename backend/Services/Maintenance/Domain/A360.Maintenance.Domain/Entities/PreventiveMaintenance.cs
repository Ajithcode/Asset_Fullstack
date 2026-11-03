
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Maintenance.Domain.Entities;

public class PreventiveMaintenance : BaseEntity
{
    [BsonElement("pm_schedule_id")]
    public string PmScheduleId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string AssetId { get; set; } = null!;

    [BsonElement("asset_name")]
    public string AssetName { get; set; } = null!;

    [BsonElement("frequency")]
    public string Frequency { get; set; } = null!;

    [BsonElement("trigger_type")]
    public string TriggerType { get; set; } = null!;

    [BsonElement("last_maintenance_date")]
    public DateTime? LastMaintenanceDate { get; set; }

    [BsonElement("next_due_date")]
    public DateTime? NextDueDate { get; set; }

    [BsonElement("auto_create_work_order")]
    public bool AutoCreateWorkOrder { get; set; }
}
