namespace A360.Tracking.Domain.Entities;

/// <summary>
/// The normalised form of a tag id, used to store new tag ids and to decide whether two tags are the same physical tag
/// (C300009AAF43 = c3:00:00:9a:af:43 = BLE-C300009AAF43).
/// </summary>
/// <remarks>
/// Rule: trim; drop a leading "BLE-" (any case) only when what follows is a 12-hex-digit MAC; remove ':', '-' and
/// whitespace; upper-case. Other labels keep their prefix, so BLE-000001 becomes BLE000001 (not 000001, which would merge
/// it with an unrelated tag numbered 000001). Lower-cased, the key equals what Real-Time Location and the asset
/// dashboard compare readings on (meta.tagid such as c3000071fe4c), so stored keys still match readings.
/// </remarks>
public static class TagIdKey
{
    private const string BlePrefix = "BLE-";
    private const int MacHexDigits = 12;

    /// <summary>The key of a tag id or tag value; null when nothing is left after normalising.</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();
        if (text.StartsWith(BlePrefix, StringComparison.OrdinalIgnoreCase) && IsMac(Strip(text[BlePrefix.Length..])))
        {
            text = text[BlePrefix.Length..];
        }

        var key = Strip(text);
        return key.Length == 0 ? null : key;
    }

    /// <summary>The keys a tag can be matched on: its tag id and its value (MAC, EPC, encoded value, UID).</summary>
    public static IReadOnlyList<string> KeysOf(TrackingTag tag)
    {
        return new[] { Normalize(tag.TagId), Normalize(tag.Value) }
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string Strip(string text)
    {
        return new string(text.Where(c => c is not (':' or '-') && !char.IsWhiteSpace(c)).Select(char.ToUpperInvariant).ToArray());
    }

    private static bool IsMac(string hex)
    {
        return hex.Length == MacHexDigits && hex.All(Uri.IsHexDigit);
    }
}
