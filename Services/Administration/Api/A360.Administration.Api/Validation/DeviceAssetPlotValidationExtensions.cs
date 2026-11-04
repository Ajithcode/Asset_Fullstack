using System.Text.RegularExpressions;
using A360.Administration.Api.Contracts;
using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Validation;

public static partial class DeviceAssetPlotValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateDeviceAssetPlotRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, nameof(request.ItemType), request.ItemType);
        Require(errors, nameof(request.ItemName), request.ItemName);
        Require(errors, nameof(request.LocationPath), request.LocationPath);

        OneOf(errors, nameof(request.ItemType), request.ItemType, DeviceAssetPlotItemType.All);
        OneOf(errors, nameof(request.Status), request.Status, DeviceAssetPlotStatus.All);

        if (request.PositionX is null)
        {
            errors[nameof(request.PositionX)] = ["PositionX is required"];
        }
        else if (request.PositionX < 0 || !double.IsFinite(request.PositionX.Value))
        {
            errors[nameof(request.PositionX)] = ["PositionX must be zero or greater"];
        }

        if (request.PositionY is null)
        {
            errors[nameof(request.PositionY)] = ["PositionY is required"];
        }
        else if (request.PositionY < 0 || !double.IsFinite(request.PositionY.Value))
        {
            errors[nameof(request.PositionY)] = ["PositionY must be zero or greater"];
        }

        if (request.Orientation is < 0 or >= 360)
        {
            errors[nameof(request.Orientation)] = ["Orientation must be between 0 and 359 degrees"];
        }

        if (!string.IsNullOrWhiteSpace(request.Color) && !HexColorPattern().IsMatch(request.Color.Trim()))
        {
            errors[nameof(request.Color)] = ["Color must be a hex colour such as #7C3AED"];
        }

        return errors;
    }

    /// <summary>The allowed value matching ignoring case, or null when missing or not allowed.</summary>
    public static string? Normalize(string? value, IReadOnlyList<string> allowed)
    {
        return allowed.FirstOrDefault(item => string.Equals(item, value?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static void Require(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
    }

    private static void OneOf(Dictionary<string, string[]> errors, string field, string? value, IReadOnlyList<string> allowed)
    {
        if (!string.IsNullOrWhiteSpace(value) && Normalize(value, allowed) is null)
        {
            errors[field] = [$"{field} must be one of: {string.Join(", ", allowed)}"];
        }
    }

    [GeneratedRegex("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})$")]
    private static partial Regex HexColorPattern();
}
