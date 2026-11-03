using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

// A stock status such as "In Stock" or "Reorder".
public class StockStatus : BaseEntity
{
    [BsonElement("status_name")]
    public string StatusName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // e.g. "Available", "Warning" or "Unavailable".
    [BsonElement("type")]
    public string Type { get; set; } = null!;

    // Hex colour used to show the status, e.g. "#2E7D32".
    [BsonElement("color")]
    public string? Color { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // What the status is used for, e.g. "Trigger reorder".
    [BsonElement("usage")]
    public string? Usage { get; set; }

    // At most one stock status per client is the default.
    [BsonElement("system_default")]
    public bool SystemDefault { get; set; }

    // Stock in this status can be issued to work orders.
    [BsonElement("can_issue")]
    public bool CanIssue { get; set; }

    [BsonElement("can_reserve")]
    public bool CanReserve { get; set; }

    // Counted in the available quantity.
    [BsonElement("counts_as_available")]
    public bool CountsAsAvailable { get; set; }

    [BsonElement("triggers_reorder")]
    public bool TriggersReorder { get; set; }

    [BsonElement("show_in_reports")]
    public bool ShowInReports { get; set; } = true;

    [BsonElement("notify_storekeeper")]
    public bool NotifyStorekeeper { get; set; }
}
