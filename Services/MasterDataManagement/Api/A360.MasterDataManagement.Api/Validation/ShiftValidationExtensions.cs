using System.Text.RegularExpressions;
using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class ShiftValidationExtensions
{
    // "#RGB" or "#RRGGBB".
    private static readonly Regex ColorPattern = new(
        "^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$",
        RegexOptions.Compiled);

    public static Dictionary<string, string[]> Validate(
        this CreateShiftRequest request)
    {
        return ValidateFields(
            request.ShiftName, request.Code, request.StartTime, request.EndTime, request.WorkingDays, request.Color,
            request.ShiftType, request.GraceMinutes, request.Breaks);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateShiftRequest request)
    {
        return ValidateFields(
            request.ShiftName, request.Code, request.StartTime, request.EndTime, request.WorkingDays, request.Color,
            request.ShiftType, request.GraceMinutes, request.Breaks);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateShiftStatusRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            errors["Status"] =
                ["Status is required"];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateFields(
        string? shiftName,
        string? code,
        string? startTime,
        string? endTime,
        List<string>? workingDays,
        string? color,
        string? shiftType,
        int? graceMinutes,
        List<ShiftBreakDto>? breaks)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(shiftName))
        {
            errors["ShiftName"] =
                ["ShiftName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        var startValid = ShiftTimes.TryParseTime(startTime, out var start);
        var endValid = ShiftTimes.TryParseTime(endTime, out var end);

        // Flexible and on-call shifts may leave both times empty.
        var openHours = !ShiftTimes.HasFixedHours(shiftType)
            && string.IsNullOrWhiteSpace(startTime)
            && string.IsNullOrWhiteSpace(endTime);

        if (!startValid && !openHours)
        {
            errors["StartTime"] =
                [$"StartTime is required as a 24-hour time ({ShiftTimes.Format}), e.g. 06:00"];
        }

        if (!endValid && !openHours)
        {
            errors["EndTime"] =
                [$"EndTime is required as a 24-hour time ({ShiftTimes.Format}), e.g. 14:00"];
        }

        if (startValid && endValid && start == end)
        {
            errors["EndTime"] =
                ["EndTime must be different from StartTime"];
        }

        if (graceMinutes is < 0 or > 120)
        {
            errors["GraceMinutes"] =
                ["GraceMinutes must be between 0 and 120"];
        }

        if (breaks is not null && breaks.Any(x => x is null || string.IsNullOrWhiteSpace(x.Name) || x.Minutes is not > 0))
        {
            errors["Breaks"] =
                ["Every break needs a name and minutes"];
        }
        else if (breaks is not null && breaks.Any(x => !string.IsNullOrWhiteSpace(x.Start) && !ShiftTimes.TryParseTime(x.Start, out _)))
        {
            errors["Breaks"] =
                [$"Break start times must be 24-hour times ({ShiftTimes.Format})"];
        }

        var invalidDays = (workingDays ?? [])
            .Where(x => !ShiftTimes.TryParseDay(x, out _))
            .ToList();

        if (invalidDays.Count > 0)
        {
            errors["WorkingDays"] =
                [$"Unknown working day(s): {string.Join(", ", invalidDays)}. Use day names such as Monday or Mon"];
        }

        if (!string.IsNullOrWhiteSpace(color) && !ColorPattern.IsMatch(color.Trim()))
        {
            errors["Color"] =
                ["Color must be a hex colour such as #1565C0"];
        }

        return errors;
    }
}
