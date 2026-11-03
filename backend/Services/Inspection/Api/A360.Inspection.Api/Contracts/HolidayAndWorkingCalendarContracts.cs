using HolidayAndWorkingCalendarEntity = A360.Inspection.Domain.Entities.HolidayAndWorkingCalendar;

namespace A360.Inspection.Api.Contracts;

public sealed record CreateHolidayAndWorkingCalendarRequest(
    string? CalendarName,
    string? Country,
    List<string>? WorkingDays,
    List<string>? Weekend,
    string? WorkingHours,
    List<string>? Holidays,
    string? ShiftTimings,
    string? SlaCalculationMethod,
    bool? IsActive,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public HolidayAndWorkingCalendarEntity ToEntity(string calendarCode)
    {
        return new HolidayAndWorkingCalendarEntity
        {
            CalendarCode = calendarCode,
            CalendarName = CalendarName ?? string.Empty,
            Country = Country ?? string.Empty,
            WorkingDays = WorkingDays ?? [],
            Weekend = Weekend ?? [],
            WorkingHours = WorkingHours ?? string.Empty,
            Holidays = Holidays ?? [],
            ShiftTimings = ShiftTimings ?? string.Empty,
            SlaCalculationMethod = SlaCalculationMethod ?? string.Empty,
            IsActive = IsActive ?? true,
            Status = Status,
            CreatedBy = CreatedBy,
            CreatedAt = DateTime.UtcNow,
            ClientId = ClientId,
            TenantId = TenantId,
            IsDeleted = false
        };
    }
}

public sealed record UpdateHolidayAndWorkingCalendarRequest(
    string? CalendarName,
    string? Country,
    List<string>? WorkingDays,
    List<string>? Weekend,
    string? WorkingHours,
    List<string>? Holidays,
    string? ShiftTimings,
    string? SlaCalculationMethod,
    bool? IsActive,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(HolidayAndWorkingCalendarEntity calendar)
    {
        calendar.CalendarName = CalendarName ?? string.Empty;
        calendar.Country = Country ?? string.Empty;
        calendar.WorkingDays = WorkingDays ?? [];
        calendar.Weekend = Weekend ?? [];
        calendar.WorkingHours = WorkingHours ?? string.Empty;
        calendar.Holidays = Holidays ?? [];
        calendar.ShiftTimings = ShiftTimings ?? string.Empty;
        calendar.SlaCalculationMethod = SlaCalculationMethod ?? string.Empty;
        calendar.IsActive = IsActive ?? calendar.IsActive;
        calendar.Status = Status;
        calendar.UpdatedBy = UpdatedBy;
        calendar.UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record HolidayAndWorkingCalendarResponse(
    string Id,
    string CalendarCode,
    string CalendarName,
    string Country,
    List<string> WorkingDays,
    List<string> Weekend,
    string WorkingHours,
    List<string> Holidays,
    string ShiftTimings,
    string SlaCalculationMethod,
    bool IsActive,
    string? Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    bool IsDeleted)
{
    public static HolidayAndWorkingCalendarResponse FromEntity(HolidayAndWorkingCalendarEntity calendar)
    {
        return new HolidayAndWorkingCalendarResponse(
            calendar.Id,
            calendar.CalendarCode,
            calendar.CalendarName,
            calendar.Country,
            calendar.WorkingDays,
            calendar.Weekend,
            calendar.WorkingHours,
            calendar.Holidays,
            calendar.ShiftTimings,
            calendar.SlaCalculationMethod,
            calendar.IsActive,
            calendar.Status,
            calendar.CreatedBy,
            calendar.CreatedAt,
            calendar.UpdatedBy,
            calendar.UpdatedAt,
            calendar.ClientId,
            calendar.TenantId,
            calendar.IsDeleted);
    }
}
