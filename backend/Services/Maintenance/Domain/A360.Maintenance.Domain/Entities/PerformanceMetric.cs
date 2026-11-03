
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Maintenance.Domain.Entities;

public class PerformanceMetric : BaseEntity
{
    [BsonElement("asset_id")]
    public string AssetId { get; set; } = null!;

    [BsonElement("asset_name")]
    public string AssetName { get; set; } = null!;

    [BsonElement("mtbf_hrs")]
    public double MtbfHrs { get; set; }

    [BsonElement("mttr_hrs")]
    public double MttrHrs { get; set; }

    [BsonElement("asset_uptime_percent")]
    public double AssetUptimePercent { get; set; }

    [BsonElement("maintenance_frequency")]
    public string? MaintenanceFrequency { get; set; }
}
