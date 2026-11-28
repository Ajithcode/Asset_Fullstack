using System.Text.RegularExpressions;

namespace NetworkServer.Services;

/// <summary>Masks secret values ("key", "password", "secret", "token") in JSON payloads before they are stored, published or logged.</summary>
public static partial class SecretMasker
{
    [GeneratedRegex("(\"(?:key|password|passwd|secret|token)\"\\s*:\\s*\")([^\"]*)(\")", RegexOptions.IgnoreCase)]
    private static partial Regex Secret();

    public static string Mask(string text) => string.IsNullOrEmpty(text) ? text : Secret().Replace(text, "$1****$3");

    /// <summary>Commands published to a gateway (/gw/{id}/action) carry the tag connection key: never keep it.</summary>
    public static bool IsCommandTopic(string? topic) => topic != null && topic.EndsWith("/action", StringComparison.OrdinalIgnoreCase);
}
