using System.Text.RegularExpressions;

namespace A360.Tracking.Api.Locating;

/// <summary>Masks secret values ("key", "password", "secret", "token") in JSON text before it is logged or stored.</summary>
public static partial class SecretMasker
{
    [GeneratedRegex("(\"(?:key|password|passwd|secret|token)\"\\s*:\\s*\")([^\"]*)(\")", RegexOptions.IgnoreCase)]
    private static partial Regex Secret();

    public static string Mask(string text) => string.IsNullOrEmpty(text) ? text : Secret().Replace(text, "$1****$3");
}
