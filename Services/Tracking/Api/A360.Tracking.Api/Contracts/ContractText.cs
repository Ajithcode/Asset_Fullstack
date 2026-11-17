namespace A360.Tracking.Api.Contracts;

internal static class ContractText
{
    public static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static string? FirstNonEmpty(string? requested, string? fallback)
    {
        return string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim();
    }

    /// <summary>The allowed value matching the input ignoring case (e.g. "passive rfid" → "Passive RFID"), or null.</summary>
    public static string? Canonical(string? value, IEnumerable<string> allowed)
    {
        var trimmed = TrimOrNull(value);
        return trimmed is null ? null : allowed.FirstOrDefault(option => string.Equals(option, trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
