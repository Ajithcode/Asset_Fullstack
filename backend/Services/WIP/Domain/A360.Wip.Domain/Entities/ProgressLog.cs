
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Wip.Domain.Entities;

public class ProgressLog : BaseEntity
{
    [BsonElement("log_id")]
    public string LogId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string? AssetId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("job_id")]
    public string? JobId { get; set; }

    [BsonElement("task_id")]
    public string? TaskId { get; set; }

    [BsonElement("timestamp")]
    public DateTime? Timestamp { get; set; }

    [BsonElement("progress_percentage")]
    public double? ProgressPercentage { get; set; }

    [BsonElement("update_source")]
    public string? UpdateSource { get; set; }

    [BsonElement("remarks")]
    public string? Remarks { get; set; }

    [BsonElement("sensor_data")]
    public string? SensorData { get; set; }
}
