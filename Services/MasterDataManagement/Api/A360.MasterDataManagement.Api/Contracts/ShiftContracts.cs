using System.Globalization;
using ShiftEntity = A360.MasterDataManagement.Domain.Entities.Shift;

namespace A360.MasterDataManagement.Api.Contracts;

public static class ShiftTimes
{
    public const string Format = "HH:mm";

    private const int MinutesPerDay = 24 * 60;

    // Monday first, matching how working days are stored and returned.
    private static readonly DayOfWeek[] WeekOrder =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    public static bool TryParseTime(string? value, out TimeOnly time)
    {
        return TimeOnly.TryParseExact(value?.Trim(), Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }

    /// <summary>Minutes from start to end; an end before the start runs past midnight.</summary>
    public static int DurationMinutes(string startTime, string endTime)
    {
        TryParseTime(startTime, out var start);
        TryParseTime(endTime, out var end);

        var minutes = (end.Hour * 60 + end.Minute) - (start.Hour * 60 + start.Minute);
        return minutes <= 0 ? minutes + MinutesPerDay : minutes;
    }

    /// <summary>Accepts full names or three-letter abbreviations in any case, e.g. "monday" or "Mon".</summary>
    public static bool TryParseDay(string? value, out DayOfWeek day)
    {
        var text = value?.Trim() ?? string.Empty;

        foreach (var candidate in WeekOrder)
        {
            var name = candidate.ToString();

            if (text.Equals(name, StringComparison.OrdinalIgnoreCase)
                || (text.Length == 3 && name.StartsWith(text, StringComparison.OrdinalIgnoreCase)))
            {
                day = candidate;
                return true;
            }
        }

        day = default;
        return false;
    }

    /// <summary>Turns valid day entries into distinct full names in Monday-to-Sunday order.</summary>
    public static List<string> NormalizeDays(IEnumerable<string>? values)
    {
        var days = (values ?? [])
            .Select(x => TryParseDay(x, out var day) ? (DayOfWeek?)day : null)
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .ToHashSet();

        return WeekOrder.Where(days.Contains).Select(x => x.ToString()).ToList();
    }
}

public sealed record CreateShiftRequest(
    string? ShiftName,
    string? Code,
    string? ShiftTime,
    string? StartTime,
    string? EndTime,
    List<string>? WorkingDays,
    string? Color,
    string? Description,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public ShiftEntity ToEntity()
    {
        return new ShiftEntity
        {
            ShiftName = ShiftName!.Trim(),
            Code = Code!.Trim(),
            ShiftTime = string.IsNullOrWhiteSpace(ShiftTime) ? null : ShiftTime.Trim(),
            StartTime = StartTime!.Trim(),
            EndTime = EndTime!.Trim(),
            Duration = ShiftTimes.DurationMinutes(StartTime!, EndTime!),
            WorkingDays = ShiftTimes.NormalizeDays(WorkingDays),
            Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateShiftRequest(
    string? ShiftName,
    string? Code,
    string? ShiftTime,
    string? StartTime,
    string? EndTime,
    List<string>? WorkingDays,
    string? Color,
    string? Description,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(ShiftEntity shift)
    {
        shift.ShiftName = ShiftName!.Trim();
        shift.Code = Code!.Trim();
        shift.ShiftTime = string.IsNullOrWhiteSpace(ShiftTime) ? null : ShiftTime.Trim();
        shift.StartTime = StartTime!.Trim();
        shift.EndTime = EndTime!.Trim();
        shift.Duration = ShiftTimes.DurationMinutes(StartTime!, EndTime!);
        shift.WorkingDays = ShiftTimes.NormalizeDays(WorkingDays);
        shift.Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant();
        shift.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

        shift.UpdatedBy = UpdatedBy;
        shift.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            shift.Status = Status;
        }
    }
}

public sealed record UpdateShiftStatusRequest(
    string? Status,
    string? UpdatedBy);

// Duration is in minutes.
public sealed record ShiftResponse(
    string Id,
    string ShiftName,
    string Code,
    string? ShiftTime,
    string StartTime,
    string EndTime,
    int Duration,
    IReadOnlyList<string> WorkingDays,
    string? Color,
    string? Description,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static ShiftResponse FromEntity(ShiftEntity shift)
    {
        return new ShiftResponse(
            shift.Id ?? string.Empty,
            shift.ShiftName ?? string.Empty,
            shift.Code ?? string.Empty,
            shift.ShiftTime,
            shift.StartTime ?? string.Empty,
            shift.EndTime ?? string.Empty,
            shift.Duration,
            shift.WorkingDays ?? [],
            shift.Color,
            shift.Description,
            shift.CreatedBy,
            shift.CreatedAt,
            shift.UpdatedBy,
            shift.UpdatedAt,
            shift.ClientId,
            shift.TenantId,
            shift.Status,
            shift.IsDeleted
        );
    }
}
