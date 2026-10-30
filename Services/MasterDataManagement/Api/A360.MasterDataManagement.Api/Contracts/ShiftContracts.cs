using A360.MasterDataManagement.Domain.ValueObjects;
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

    /// <summary>Shift types without fixed hours or working days.</summary>
    public static readonly string[] OpenTypes = ["Flexible", "On Call"];

    public static bool HasFixedHours(string? shiftType)
    {
        return !OpenTypes.Contains(shiftType?.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Minutes from start to end; an end before the start runs past midnight. 0 without both times.</summary>
    public static int DurationMinutes(string? startTime, string? endTime)
    {
        if (!TryParseTime(startTime, out var start) || !TryParseTime(endTime, out var end))
        {
            return 0;
        }

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
    string? ShiftType,
    DateOnly? EffectiveFrom,
    int? GraceMinutes,
    string? RotationPattern,
    List<ShiftBreakDto>? Breaks,
    bool? OvertimeAllowed,
    bool? NightAllowance,
    bool? AttendanceRequired,
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
            StartTime = StructureText.Clean(StartTime),
            EndTime = StructureText.Clean(EndTime),
            Duration = ShiftTimes.DurationMinutes(StartTime, EndTime),
            WorkingDays = ShiftTimes.NormalizeDays(WorkingDays),
            Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            ShiftType = StructureText.Clean(ShiftType),
            EffectiveFrom = EffectiveFrom,
            GraceMinutes = GraceMinutes ?? 0,
            RotationPattern = StructureText.Clean(RotationPattern),
            Breaks = ShiftBreakDto.ToValues(Breaks),
            OvertimeAllowed = OvertimeAllowed ?? false,
            NightAllowance = NightAllowance ?? false,
            AttendanceRequired = AttendanceRequired ?? false,

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
    string? ShiftType,
    DateOnly? EffectiveFrom,
    int? GraceMinutes,
    string? RotationPattern,
    List<ShiftBreakDto>? Breaks,
    bool? OvertimeAllowed,
    bool? NightAllowance,
    bool? AttendanceRequired,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(ShiftEntity shift)
    {
        shift.ShiftName = ShiftName!.Trim();
        shift.Code = Code!.Trim();
        shift.ShiftTime = string.IsNullOrWhiteSpace(ShiftTime) ? null : ShiftTime.Trim();
        shift.StartTime = StructureText.Clean(StartTime);
        shift.EndTime = StructureText.Clean(EndTime);
        shift.Duration = ShiftTimes.DurationMinutes(StartTime, EndTime);
        shift.WorkingDays = ShiftTimes.NormalizeDays(WorkingDays);
        shift.Color = string.IsNullOrWhiteSpace(Color) ? null : Color.Trim().ToUpperInvariant();
        shift.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        shift.ShiftType = StructureText.Clean(ShiftType);
        shift.EffectiveFrom = EffectiveFrom;
        shift.GraceMinutes = GraceMinutes ?? 0;
        shift.RotationPattern = StructureText.Clean(RotationPattern);
        shift.Breaks = ShiftBreakDto.ToValues(Breaks);
        shift.OvertimeAllowed = OvertimeAllowed ?? false;
        shift.NightAllowance = NightAllowance ?? false;
        shift.AttendanceRequired = AttendanceRequired ?? false;

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

// Duration is in minutes; 0 (and empty times) for shifts without fixed hours.
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
    string? ShiftType,
    DateOnly? EffectiveFrom,
    int GraceMinutes,
    string? RotationPattern,
    IReadOnlyList<ShiftBreakDto> Breaks,
    bool OvertimeAllowed,
    bool NightAllowance,
    bool AttendanceRequired,
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
            shift.ShiftType,
            shift.EffectiveFrom,
            shift.GraceMinutes,
            shift.RotationPattern,
            ShiftBreakDto.FromValues(shift.Breaks),
            shift.OvertimeAllowed,
            shift.NightAllowance,
            shift.AttendanceRequired,
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

public sealed record ShiftBreakDto(
    string? Name,
    string? Start,
    int? Minutes)
{
    public ShiftBreak ToValue()
    {
        return new ShiftBreak
        {
            Name = Name?.Trim() ?? string.Empty,
            Start = StructureText.Clean(Start),
            Minutes = Minutes ?? 0
        };
    }

    public static List<ShiftBreak> ToValues(IEnumerable<ShiftBreakDto>? breaks)
    {
        return breaks?.Select(x => x.ToValue()).ToList() ?? [];
    }

    public static IReadOnlyList<ShiftBreakDto> FromValues(IEnumerable<ShiftBreak>? breaks)
    {
        return breaks?.Select(x => new ShiftBreakDto(x.Name, x.Start, x.Minutes)).ToList() ?? [];
    }
}
