using A360.Inspection.Domain.Entities;
using HolidayAndWorkingCalendarEntity = A360.Inspection.Domain.Entities.HolidayAndWorkingCalendar;

namespace A360.Inspection.Api.Contracts;

/// <summary>Editable fields shared by the create and update requests for HolidayAndWorkingCalendar.</summary>
public abstract record HolidayAndWorkingCalendarFields
{
    public string? CalendarName { get; init; }

    public string? SiteName { get; init; }

    public string? Timezone { get; init; }

    public string? EffectiveFrom { get; init; }

    public List<WorkingDay>? WorkingWeek { get; init; }

    public string? BreakPeriod { get; init; }

    public List<CalendarException>? Exceptions { get; init; }

    public string? NonWorkingDayRule { get; init; }

    public string? DueDateCalculation { get; init; }

    public string? SlaCalendar { get; init; }

    public bool? SkipHolidays { get; init; }

    public bool? RespectInspectorShift { get; init; }

    public bool? AllowManualOverride { get; init; }

    public int? AssignedPlans { get; init; }

    public int? AssignedInspectors { get; init; }

    public int? AssignedSites { get; init; }

    public bool? IsActive { get; init; }

    public string? Country { get; init; }

    public List<string>? WorkingDays { get; init; }

    public List<string>? Weekend { get; init; }

    public string? WorkingHours { get; init; }

    public List<string>? Holidays { get; init; }

    public string? ShiftTimings { get; init; }

    public string? SlaCalculationMethod { get; init; }

    public string? Status { get; init; }

    protected void ApplyFields(HolidayAndWorkingCalendarEntity calendar)
    {
        calendar.CalendarName = CalendarName ?? calendar.CalendarName;
        calendar.SiteName = SiteName ?? calendar.SiteName;
        calendar.Timezone = Timezone ?? calendar.Timezone;
        calendar.EffectiveFrom = EffectiveFrom;
        calendar.WorkingWeek = WorkingWeek ?? calendar.WorkingWeek;
        calendar.BreakPeriod = BreakPeriod ?? calendar.BreakPeriod;
        calendar.Exceptions = Exceptions ?? calendar.Exceptions;
        calendar.NonWorkingDayRule = NonWorkingDayRule ?? calendar.NonWorkingDayRule;
        calendar.DueDateCalculation = DueDateCalculation ?? calendar.DueDateCalculation;
        calendar.SlaCalendar = SlaCalendar ?? calendar.SlaCalendar;
        calendar.SkipHolidays = SkipHolidays ?? calendar.SkipHolidays;
        calendar.RespectInspectorShift = RespectInspectorShift ?? calendar.RespectInspectorShift;
        calendar.AllowManualOverride = AllowManualOverride ?? calendar.AllowManualOverride;
        calendar.AssignedPlans = AssignedPlans ?? calendar.AssignedPlans;
        calendar.AssignedInspectors = AssignedInspectors ?? calendar.AssignedInspectors;
        calendar.AssignedSites = AssignedSites ?? calendar.AssignedSites;
        calendar.IsActive = IsActive ?? calendar.IsActive;
        calendar.Country = Country ?? calendar.Country;
        calendar.WorkingDays = WorkingDays ?? calendar.WorkingDays;
        calendar.Weekend = Weekend ?? calendar.Weekend;
        calendar.WorkingHours = WorkingHours ?? calendar.WorkingHours;
        calendar.Holidays = Holidays ?? calendar.Holidays;
        calendar.ShiftTimings = ShiftTimings ?? calendar.ShiftTimings;
        calendar.SlaCalculationMethod = SlaCalculationMethod ?? calendar.SlaCalculationMethod;
    }
}

public sealed record CreateHolidayAndWorkingCalendarRequest : HolidayAndWorkingCalendarFields
{
    public string? CreatedBy { get; init; }

    public string? ClientId { get; init; }

    public string? TenantId { get; init; }

    public HolidayAndWorkingCalendarEntity ToEntity(string calendarCode)
    {
        var calendar = new HolidayAndWorkingCalendarEntity
        {
            CalendarCode = calendarCode,
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };

        ApplyFields(calendar);
        return calendar;
    }
}

public sealed record UpdateHolidayAndWorkingCalendarRequest : HolidayAndWorkingCalendarFields
{
    public string? UpdatedBy { get; init; }

    public void ApplyTo(HolidayAndWorkingCalendarEntity calendar)
    {
        ApplyFields(calendar);
        calendar.Status = Status ?? calendar.Status;
        calendar.UpdatedBy = UpdatedBy;
        calendar.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record HolidayAndWorkingCalendarResponse : AuditResponse
{
    private HolidayAndWorkingCalendarResponse(HolidayAndWorkingCalendarEntity calendar)
        : base(calendar)
    {
    }

    public string CalendarCode { get; init; } = string.Empty;

    public string CalendarName { get; init; } = default!;

    public string SiteName { get; init; } = default!;

    public string Timezone { get; init; } = default!;

    public string? EffectiveFrom { get; init; }

    public List<WorkingDay> WorkingWeek { get; init; } = default!;

    public string BreakPeriod { get; init; } = default!;

    public List<CalendarException> Exceptions { get; init; } = default!;

    public string NonWorkingDayRule { get; init; } = default!;

    public string DueDateCalculation { get; init; } = default!;

    public string SlaCalendar { get; init; } = default!;

    public bool SkipHolidays { get; init; }

    public bool RespectInspectorShift { get; init; }

    public bool AllowManualOverride { get; init; }

    public int AssignedPlans { get; init; }

    public int AssignedInspectors { get; init; }

    public int AssignedSites { get; init; }

    public bool IsActive { get; init; }

    public string Country { get; init; } = default!;

    public List<string> WorkingDays { get; init; } = default!;

    public List<string> Weekend { get; init; } = default!;

    public string WorkingHours { get; init; } = default!;

    public List<string> Holidays { get; init; } = default!;

    public string ShiftTimings { get; init; } = default!;

    public string SlaCalculationMethod { get; init; } = default!;

    public static HolidayAndWorkingCalendarResponse FromEntity(HolidayAndWorkingCalendarEntity calendar)
    {
        return new HolidayAndWorkingCalendarResponse(calendar)
        {
            CalendarCode = calendar.CalendarCode,
            CalendarName = calendar.CalendarName,
            SiteName = calendar.SiteName,
            Timezone = calendar.Timezone,
            EffectiveFrom = calendar.EffectiveFrom,
            WorkingWeek = calendar.WorkingWeek,
            BreakPeriod = calendar.BreakPeriod,
            Exceptions = calendar.Exceptions,
            NonWorkingDayRule = calendar.NonWorkingDayRule,
            DueDateCalculation = calendar.DueDateCalculation,
            SlaCalendar = calendar.SlaCalendar,
            SkipHolidays = calendar.SkipHolidays,
            RespectInspectorShift = calendar.RespectInspectorShift,
            AllowManualOverride = calendar.AllowManualOverride,
            AssignedPlans = calendar.AssignedPlans,
            AssignedInspectors = calendar.AssignedInspectors,
            AssignedSites = calendar.AssignedSites,
            IsActive = calendar.IsActive,
            Country = calendar.Country,
            WorkingDays = calendar.WorkingDays,
            Weekend = calendar.Weekend,
            WorkingHours = calendar.WorkingHours,
            Holidays = calendar.Holidays,
            ShiftTimings = calendar.ShiftTimings,
            SlaCalculationMethod = calendar.SlaCalculationMethod
        };
    }
}
