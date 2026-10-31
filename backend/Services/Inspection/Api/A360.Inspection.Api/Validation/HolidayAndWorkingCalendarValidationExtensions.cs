using A360.Inspection.Api.Contracts;

namespace A360.Inspection.Api.Validation;

public static class HolidayAndWorkingCalendarValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateHolidayAndWorkingCalendarRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.CalendarName))
        {
            errors["CalendarName"] = ["CalendarName is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Country))
        {
            errors["Country"] = ["Country is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateHolidayAndWorkingCalendarRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.CalendarName))
        {
            errors["CalendarName"] = ["CalendarName is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Country))
        {
            errors["Country"] = ["Country is required"];
        }

        return errors;
    }
}
