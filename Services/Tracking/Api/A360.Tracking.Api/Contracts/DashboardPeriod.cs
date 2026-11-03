namespace A360.Tracking.Api.Contracts;

/// <summary>
/// The "last N days" window used by the IoT Tracking Dashboard: whole UTC days, ending with today.
/// For days = 7 on 2 Nov the window is 27 Oct 00:00 to 3 Nov 00:00 (exclusive), and the previous period is the 7 days before.
/// </summary>
public sealed record DashboardPeriod(int Days, DateTime FromUtc, DateTime ToUtc)
{
    public const int DefaultDays = 7;
    public const int MaxDays = 365;

    public DateTime PreviousFromUtc => FromUtc.AddDays(-Days);

    public static DashboardPeriod LastDays(int? days)
    {
        var length = days ?? DefaultDays;
        var toUtc = DateTime.UtcNow.Date.AddDays(1);
        return new DashboardPeriod(length, toUtc.AddDays(-length), toUtc);
    }

    /// <summary>Null when valid; otherwise a validation problem for the "days" query parameter.</summary>
    public static IResult? Invalid(int? days)
    {
        return days is null or (>= 1 and <= MaxDays)
            ? null
            : Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["days"] = [$"days must be between 1 and {MaxDays}"]
            });
    }

    /// <summary>Every day of the window as yyyy-MM-dd, oldest first.</summary>
    public IEnumerable<string> EachDay()
    {
        for (var day = FromUtc; day < ToUtc; day = day.AddDays(1))
        {
            yield return day.ToString("yyyy-MM-dd");
        }
    }

    /// <summary>Change from the previous period in percent, rounded to 1 decimal; null when the previous period had none.</summary>
    public static double? ChangePercent(int current, int previous)
    {
        return previous == 0 ? null : Math.Round((current - previous) * 100.0 / previous, 1);
    }
}
