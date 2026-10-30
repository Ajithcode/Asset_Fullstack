
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Maintenance.Domain.Entities;

public class DowntimeRecord : BaseEntity
{
    [BsonElement("asset_id")]
    public string AssetId { get; set; } = null!;

    [BsonElement("asset_name")]
    public string AssetName { get; set; } = null!;

    [BsonElement("downtime_start")]
    public DateTime? DowntimeStart { get; set; }

    [BsonElement("downtime_end")]
    public DateTime? DowntimeEnd { get; set; }

    [BsonElement("total_downtime")]
    public string? TotalDowntime { get; set; }

    [BsonElement("reason_for_downtime")]
    public string? ReasonForDowntime { get; set; }

    [BsonElement("impact_level")]
    public string? ImpactLevel { get; set; }
}
