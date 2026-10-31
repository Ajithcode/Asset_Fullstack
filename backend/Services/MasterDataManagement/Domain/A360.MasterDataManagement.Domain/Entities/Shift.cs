using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

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

    // 24-hour "HH:mm"; empty for shifts without fixed hours. An end time before the start time means the shift runs past midnight.
    [BsonElement("start_time")]
    public string? StartTime { get; set; }

    [BsonElement("end_time")]
    public string? EndTime { get; set; }

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

    // "Regular", "Rotational", "Flexible" or "On Call". Flexible and on-call shifts have no fixed hours.
    [BsonElement("shift_type")]
    public string? ShiftType { get; set; }

    [BsonElement("effective_from")]
    public DateOnly? EffectiveFrom { get; set; }

    // Minutes someone may arrive late without being marked late.
    [BsonElement("grace_minutes")]
    public int GraceMinutes { get; set; }

    [BsonElement("rotation_pattern")]
    public string? RotationPattern { get; set; }

    [BsonElement("breaks")]
    public List<ShiftBreak> Breaks { get; set; } = [];

    [BsonElement("overtime_allowed")]
    public bool OvertimeAllowed { get; set; }

    [BsonElement("night_allowance")]
    public bool NightAllowance { get; set; }

    [BsonElement("attendance_required")]
    public bool AttendanceRequired { get; set; }
}
