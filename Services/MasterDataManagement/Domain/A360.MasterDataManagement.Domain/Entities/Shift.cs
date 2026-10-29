using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

public class Shift : BaseEntity
{
    [BsonElement("shift_name")]
    public string ShiftName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // A label for the part of the day, e.g. "Morning" or "Night".
    [BsonElement("shift_time")]
    public string? ShiftTime { get; set; }

    // 24-hour "HH:mm". An end time before the start time means the shift runs past midnight.
    [BsonElement("start_time")]
    public string StartTime { get; set; } = null!;

    [BsonElement("end_time")]
    public string EndTime { get; set; } = null!;

    // Minutes from start to end, worked out from the times.
    [BsonElement("duration")]
    public int Duration { get; set; }

    // Full day names, Monday first, e.g. ["Monday", "Tuesday"].
    [BsonElement("working_days")]
    public List<string> WorkingDays { get; set; } = [];

    // Hex colour used to show the shift, e.g. "#1565C0".
    [BsonElement("color")]
    public string? Color { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }
}
