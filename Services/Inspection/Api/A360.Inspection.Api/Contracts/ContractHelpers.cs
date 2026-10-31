using System.Globalization;

namespace A360.Inspection.Api.Contracts;

/// <summary>Converts between stored dates and the 'yyyy-MM-dd' strings the frontend sends and expects.</summary>
public static class ContractDates
{
    private const string DateFormat = "yyyy-MM-dd";

    public static string? ToDateString(DateTime? value)
    {
        return value?.ToString(DateFormat, CultureInfo.InvariantCulture);
    }

    public static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length >= DateFormat.Length
            && DateTime.TryParseExact(trimmed[..DateFormat.Length], DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            // Stored as UTC midnight so the calendar day never shifts with the server's time zone.
            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }

        return DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }
}

public static class SignatureAndStampKinds
{
    public const string Signature = "Signature";
    public const string Stamp = "Stamp";

    public static bool IsStamp(string? kind)
    {
        return string.Equals(kind?.Trim(), Stamp, StringComparison.OrdinalIgnoreCase);
    }
}

public static class NotificationChannels
{
    public static List<string> Split(string? channel)
    {
        return string.IsNullOrWhiteSpace(channel)
            ? []
            : channel.Split(new[] { ',', ';', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}
