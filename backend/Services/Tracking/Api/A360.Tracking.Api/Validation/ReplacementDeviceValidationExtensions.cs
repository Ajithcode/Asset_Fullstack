using A360.Tracking.Api.Contracts;
using A360.Tracking.Domain.Entities;

namespace A360.Tracking.Api.Validation;

public static class ReplacementDeviceValidationExtensions
{
    private const int CodeMaxLength = 50;
    private const int NameMaxLength = 100;

    public static Dictionary<string, string[]> Validate(this CreateReplacementDeviceRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "DeviceId", request.DeviceId, CodeMaxLength);
        RequireText(errors, "Model", request.Model, NameMaxLength);
        RequireText(errors, "LocationName", request.LocationName, NameMaxLength);

        OneOf(errors, "Availability", request.Availability, ReplacementAvailability.All, required: false);
        OneOf(errors, "Compatibility", request.Compatibility, ReplacementCompatibility.All, required: true);

        MaxLength(errors, "Protocol", request.Protocol, CodeMaxLength);

        if (request.BatteryLevel is < 0 or > 100)
        {
            errors["BatteryLevel"] = ["BatteryLevel must be between 0 and 100"];
        }

        return errors;
    }

    private static void RequireText(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
        else
        {
            MaxLength(errors, field, value, maxLength);
        }
    }

    private static void OneOf(Dictionary<string, string[]> errors, string field, string? value, IReadOnlyList<string> allowed, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                errors[field] = [$"{field} is required"];
            }
        }
        else if (ContractText.Canonical(value, allowed) is null)
        {
            errors[field] = [$"{field} must be one of: {string.Join(", ", allowed)}"];
        }
    }

    private static void MaxLength(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (value?.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} cannot be longer than {maxLength} characters"];
        }
    }
}
