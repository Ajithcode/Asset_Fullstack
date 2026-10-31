
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Maintenance.Domain.Entities;

public class WorkOrder : BaseEntity
{
    [BsonElement("work_order_id")]
    public string WorkOrderId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string AssetId { get; set; } = null!;

    [BsonElement("asset_name")]
    public string AssetName { get; set; } = null!;

    [BsonElement("work_type")]
    public string WorkType { get; set; } = null!;

    [BsonElement("title")]
    public string Title { get; set; } = null!;

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("priority")]
    public string Priority { get; set; } = null!;

    [BsonElement("scheduled_date")]
    public DateTime? ScheduledDate { get; set; }

    [BsonElement("assigned_technician")]
    public string? AssignedTechnician { get; set; }
}
