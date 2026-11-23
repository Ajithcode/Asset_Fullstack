using System.Text.Json;
using A360.ReportAnalytics.Api.Contracts;
using A360.ReportAnalytics.Domain.Entities;
using A360.ReportAnalytics.Api.Validation;
using MimeKit;

namespace A360.ReportAnalytics.Api.Scheduling;

/// <summary>Checks the Schedule Report form and turns it into a <see cref="StandardReportSchedule"/>.</summary>
public static class ReportScheduleValidation
{
    /// <summary>
    /// Errors keyed by field, or the schedule to save. Frequency "On demand" (or "None") turns emailing off: Schedule is null
    /// and Off is true.
    /// </summary>
    public static (Dictionary<string, string[]> Errors, StandardReportSchedule? Schedule, bool Off) Validate(ScheduleStandardReportRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Frequency))
        {
            errors["Frequency"] = ["Frequency is required"];
            return (errors, null, false);
        }

        if (StandardReportOptions.IsOnDemand(request.Frequency) || string.Equals(request.Frequency.Trim(), "None", StringComparison.OrdinalIgnoreCase))
        {
            if (request.TimeZone is not null)
            {
                StandardReportValidationExtensions.ValidateTimeZone(errors, request.TimeZone);
            }

            return (errors, null, errors.Count == 0);
        }

        var frequency = ReportScheduleOptions.NormalizeFrequency(request.Frequency);
        if (frequency is null)
        {
            errors["Frequency"] = [$"Frequency must be one of: {string.Join(", ", ReportScheduleOptions.Frequencies)}, or On demand to stop the schedule"];
        }

        var weekDays = new List<string>();
        var monthDays = new List<int>();
        var days = (request.Days ?? []).Select(DayText).Where(day => !string.IsNullOrWhiteSpace(day)).ToList();

        if (frequency == ReportScheduleOptions.Weekly)
        {
            foreach (var day in days)
            {
                if (ReportScheduleOptions.NormalizeWeekDay(day) is { } weekDay)
                {
                    if (!weekDays.Contains(weekDay)) weekDays.Add(weekDay);
                }
                else
                {
                    errors["Days"] = [$"'{day}' is not a week day; use {string.Join(", ", ReportScheduleOptions.WeekDays)}"];
                }
            }

            if (weekDays.Count == 0 && !errors.ContainsKey("Days"))
            {
                errors["Days"] = ["Pick at least one day of the week for a Weekly schedule"];
            }
        }
        else if (frequency == ReportScheduleOptions.Monthly)
        {
            foreach (var day in days)
            {
                if (int.TryParse(day, out var number) && number is >= 1 and <= 31)
                {
                    if (!monthDays.Contains(number)) monthDays.Add(number);
                }
                else
                {
                    errors["Days"] = [$"'{day}' is not a day of the month; use 1 to 31"];
                }
            }

            if (monthDays.Count == 0 && !errors.ContainsKey("Days"))
            {
                errors["Days"] = ["Pick at least one date (1-31) for a Monthly schedule"];
            }
        }

        var time = ReportScheduleOptions.NormalizeTime(request.Time);
        if (time is null)
        {
            errors["Time"] = [string.IsNullOrWhiteSpace(request.Time) ? "Time is required" : $"'{request.Time}' is not a valid time; use HH:mm, e.g. 09:30"];
        }

        var format = ReportScheduleOptions.NormalizeFormat(request.Format);
        if (format is null)
        {
            errors["Format"] = [string.IsNullOrWhiteSpace(request.Format) ? "Format is required" : "Format must be xlsx (Excel), csv or pdf"];
        }

        var recipients = new List<string>();
        foreach (var address in (request.Recipients ?? []).SelectMany(SplitAddresses))
        {
            if (MailboxAddress.TryParse(address, out var mailbox) && mailbox.Address.Contains('@') && mailbox.Address.Split('@')[1].Contains('.'))
            {
                if (!recipients.Contains(mailbox.Address, StringComparer.OrdinalIgnoreCase)) recipients.Add(mailbox.Address);
            }
            else
            {
                errors["Recipients"] = [$"'{address}' is not a valid email address"];
            }
        }

        if (recipients.Count == 0 && !errors.ContainsKey("Recipients"))
        {
            errors["Recipients"] = ["Add at least one recipient email address"];
        }
        else if (recipients.Count > ReportScheduleOptions.MaxRecipients)
        {
            errors["Recipients"] = [$"No more than {ReportScheduleOptions.MaxRecipients} recipients"];
        }

        if (request.TimeZone is not null)
        {
            StandardReportValidationExtensions.ValidateTimeZone(errors, request.TimeZone);
        }

        if (errors.Count > 0)
        {
            return (errors, null, false);
        }

        return (errors, new StandardReportSchedule
        {
            Enabled = true,
            Frequency = frequency!,
            WeekDays = weekDays,
            MonthDays = monthDays.Order().ToList(),
            Time = time!,
            Format = format!,
            Recipients = recipients
        }, false);
    }

    // Days may come as numbers (15) or text ("15", "Monday").
    private static string? DayText(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.String => element.GetString()?.Trim(),
        _ => null
    };

    // "a@x.com; b@y.com" in one entry is taken as two addresses.
    private static IEnumerable<string> SplitAddresses(string? value) =>
        (value ?? string.Empty).Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
