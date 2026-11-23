using System.Globalization;
using A360.ReportAnalytics.Domain.Entities;

namespace A360.ReportAnalytics.Api.Scheduling;

/// <summary>The choices of the Schedule Report form.</summary>
public static class ReportScheduleOptions
{
    public const string Daily = "Daily";
    public const string Weekly = "Weekly";
    public const string Monthly = "Monthly";

    public const string Xlsx = "xlsx";
    public const string Csv = "csv";
    public const string Pdf = "pdf";

    public static readonly IReadOnlyList<string> Frequencies = [Daily, Weekly, Monthly];

    public static readonly IReadOnlyList<string> Formats = [Xlsx, Csv, Pdf];

    public static readonly IReadOnlyList<string> WeekDays = Enum.GetValues<DayOfWeek>()
        .OrderBy(day => ((int)day + 6) % 7) // Monday first
        .Select(day => day.ToString())
        .ToList();

    public const int MaxRecipients = 50;

    public static string? NormalizeFrequency(string? value) =>
        Frequencies.FirstOrDefault(item => string.Equals(item, value?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>xlsx, csv or pdf; "excel" is taken as xlsx.</summary>
    public static string? NormalizeFormat(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "xlsx" or "excel" or "xls" => Xlsx,
        "csv" => Csv,
        "pdf" => Pdf,
        _ => null
    };

    /// <summary>"Monday", "mon" or 1 (Monday) … 7 (Sunday).</summary>
    public static string? NormalizeWeekDay(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (int.TryParse(text, out var number))
        {
            return number is >= 1 and <= 7 ? WeekDays[number - 1] : null;
        }

        return WeekDays.FirstOrDefault(day => day.StartsWith(text, StringComparison.OrdinalIgnoreCase) && text.Length >= 3);
    }

    /// <summary>"09:30", "9:30" or "09:30:00" → "09:30".</summary>
    public static string? NormalizeTime(string? value) =>
        TimeOnly.TryParseExact(value?.Trim(), ["HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time.ToString("HH:mm", CultureInfo.InvariantCulture)
            : null;
}

/// <summary>Works out when a schedule runs next.</summary>
public static class ReportScheduleCalculator
{
    /// <summary>The first run strictly after afterUtc, in UTC; null when the schedule can never run.</summary>
    public static DateTime? Next(StandardReportSchedule schedule, TimeZoneInfo timeZone, DateTime afterUtc)
    {
        var time = TimeOnly.ParseExact(schedule.Time, "HH:mm", CultureInfo.InvariantCulture);
        var afterLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(afterUtc, DateTimeKind.Utc), timeZone);
        var date = DateOnly.FromDateTime(afterLocal);

        // A monthly schedule always matches within two months; a year is a safe bound.
        for (var step = 0; step < 400; step++, date = date.AddDays(1))
        {
            if (!Matches(schedule, date))
            {
                continue;
            }

            var local = date.ToDateTime(time);
            while (timeZone.IsInvalidTime(local)) // the clock skips this time (daylight saving): run when it resumes
            {
                local = local.AddMinutes(30);
            }

            var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), timeZone);
            if (utc > afterUtc)
            {
                return utc;
            }
        }

        return null;
    }

    /// <summary>
    /// The data period a run covers, as local dates (inclusive): Daily → the day before the run, Weekly → the 7 days
    /// before the run day, Monthly → the previous calendar month.
    /// </summary>
    public static (DateOnly Start, DateOnly End) PeriodFor(string frequency, DateOnly runDate) => frequency switch
    {
        ReportScheduleOptions.Weekly => (runDate.AddDays(-7), runDate.AddDays(-1)),
        ReportScheduleOptions.Monthly => (new DateOnly(runDate.Year, runDate.Month, 1).AddMonths(-1), new DateOnly(runDate.Year, runDate.Month, 1).AddDays(-1)),
        _ => (runDate.AddDays(-1), runDate.AddDays(-1))
    };

    private static bool Matches(StandardReportSchedule schedule, DateOnly date) => schedule.Frequency switch
    {
        ReportScheduleOptions.Daily => true,
        ReportScheduleOptions.Weekly => schedule.WeekDays.Contains(date.DayOfWeek.ToString(), StringComparer.OrdinalIgnoreCase),
        ReportScheduleOptions.Monthly => schedule.MonthDays.Any(day => Math.Min(day, DateTime.DaysInMonth(date.Year, date.Month)) == date.Day),
        _ => false
    };
}
