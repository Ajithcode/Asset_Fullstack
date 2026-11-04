using System.Text.RegularExpressions;
using A360.Administration.Api.Contracts;
using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Validation;

public static partial class ZoneGeofenceValidationExtensions
{
    private const int NameMaxLength = 150;
    private const int CodeMaxLength = 50;
    private const int DescriptionMaxLength = 1000;

    public static Dictionary<string, string[]> Validate(this CreateZoneGeofenceRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, nameof(request.ZoneName), request.ZoneName, NameMaxLength);
        Require(errors, nameof(request.ZoneCode), request.ZoneCode, CodeMaxLength);
        Require(errors, nameof(request.ZoneType), request.ZoneType, NameMaxLength);

        if (!errors.ContainsKey(nameof(request.ZoneCode)) && !ZoneCodePattern().IsMatch(request.ZoneCode!.Trim()))
        {
            errors[nameof(request.ZoneCode)] = ["ZoneCode can only contain letters, digits, '-' and '_'"];
        }

        OneOf(errors, nameof(request.ZoneType), request.ZoneType, MapZoneType.All);
        OneOf(errors, nameof(request.AccessRule), request.AccessRule, MapZoneAccessRule.All);
        OneOf(errors, nameof(request.Status), request.Status, MapZoneStatus.All);

        NotNegative(errors, nameof(request.Area), request.Area);
        NotNegative(errors, nameof(request.Perimeter), request.Perimeter);

        if (request.Description?.Trim().Length > DescriptionMaxLength)
        {
            errors[nameof(request.Description)] = [$"Description must be at most {DescriptionMaxLength} characters"];
        }

        return errors;
    }

    /// <summary>The allowed value matching ignoring case, or null when missing or not allowed.</summary>
    public static string? Normalize(string? value, IReadOnlyList<string> allowed)
    {
        return allowed.FirstOrDefault(item => string.Equals(item, value?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static void Require(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
        else if (value.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} must be at most {maxLength} characters"];
        }
    }

    private static void OneOf(Dictionary<string, string[]> errors, string field, string? value, IReadOnlyList<string> allowed)
    {
        if (!string.IsNullOrWhiteSpace(value) && Normalize(value, allowed) is null)
        {
            errors[field] = [$"{field} must be one of: {string.Join(", ", allowed)}"];
        }
    }

    private static void NotNegative(Dictionary<string, string[]> errors, string field, double? value)
    {
        if (value is not null && (value < 0 || !double.IsFinite(value.Value)))
        {
            errors[field] = [$"{field} must be zero or greater"];
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex ZoneCodePattern();
}
