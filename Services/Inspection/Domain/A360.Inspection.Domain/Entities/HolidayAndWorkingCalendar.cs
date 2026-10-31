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

    [BsonElement("site_name")]
    public string SiteName { get; set; } = string.Empty;

    [BsonElement("timezone")]
    public string Timezone { get; set; } = string.Empty;

    [BsonElement("effective_from")]
    public string? EffectiveFrom { get; set; }

    [BsonElement("working_week")]
    public List<WorkingDay> WorkingWeek { get; set; } = [];

    [BsonElement("break_period")]
    public string BreakPeriod { get; set; } = string.Empty;

    [BsonElement("exceptions")]
    public List<CalendarException> Exceptions { get; set; } = [];

    [BsonElement("non_working_day_rule")]
    public string NonWorkingDayRule { get; set; } = string.Empty;

    [BsonElement("due_date_calculation")]
    public string DueDateCalculation { get; set; } = string.Empty;

    [BsonElement("sla_calendar")]
    public string SlaCalendar { get; set; } = string.Empty;

    [BsonElement("skip_holidays")]
    public bool SkipHolidays { get; set; }

    [BsonElement("respect_inspector_shift")]
    public bool RespectInspectorShift { get; set; }

    [BsonElement("allow_manual_override")]
    public bool AllowManualOverride { get; set; }

    [BsonElement("assigned_plans")]
    public int AssignedPlans { get; set; }

    [BsonElement("assigned_inspectors")]
    public int AssignedInspectors { get; set; }

    [BsonElement("assigned_sites")]
    public int AssignedSites { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class WorkingDay
{
    [BsonElement("day")]
    public string Day { get; set; } = string.Empty;

    [BsonElement("working")]
    public bool Working { get; set; }

    [BsonElement("start")]
    public string Start { get; set; } = string.Empty;

    [BsonElement("end")]
    public string End { get; set; } = string.Empty;
}

[BsonIgnoreExtraElements]
public sealed class CalendarException
{
    [BsonElement("date")]
    public string Date { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("duration")]
    public string Duration { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;
}
