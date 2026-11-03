using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Inspection.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class HolidayAndWorkingCalendar : BaseEntity
{
    [BsonElement("calendar_code")]
    public string CalendarCode { get; set; } = string.Empty;

    [BsonElement("calendar_name")]
    public string CalendarName { get; set; } = string.Empty;

    [BsonElement("country")]
    public string Country { get; set; } = string.Empty;

    [BsonElement("working_days")]
    public List<string> WorkingDays { get; set; } = [];

    [BsonElement("weekend")]
    public List<string> Weekend { get; set; } = [];

    [BsonElement("working_hours")]
    public string WorkingHours { get; set; } = string.Empty;

    [BsonElement("holidays")]
    public List<string> Holidays { get; set; } = [];

    [BsonElement("shift_timings")]
    public string ShiftTimings { get; set; } = string.Empty;

    [BsonElement("sla_calculation_method")]
    public string SlaCalculationMethod { get; set; } = string.Empty;

    [BsonElement("is_active")]
    public bool IsActive { get; set; } = true;
}
