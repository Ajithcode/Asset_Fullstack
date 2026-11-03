using A360.Tracking.Api.Contracts;
using A360.Tracking.Domain.Entities;

namespace A360.Tracking.Api.Validation;

public static class IoTDeviceValidationExtensions
{
    private const int CodeMaxLength = 50;
    private const int NameMaxLength = 100;

    public static Dictionary<string, string[]> Validate(this CreateIoTDeviceRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "DeviceId", request.DeviceId, CodeMaxLength);
        RequireText(errors, "DeviceName", request.DeviceName, NameMaxLength);
        RequireText(errors, "DeviceType", request.DeviceType, NameMaxLength);
        RequireText(errors, "Model", request.Model, NameMaxLength);
        RequireText(errors, "SerialNumber", request.SerialNumber, CodeMaxLength);
        RequireText(errors, "Connectivity", request.Connectivity, CodeMaxLength);

        MaxLength(errors, "AssetRegistrationId", request.AssetRegistrationId, CodeMaxLength);
        MaxLength(errors, "AssetCode", request.AssetCode, CodeMaxLength);
        MaxLength(errors, "AssetName", request.AssetName, NameMaxLength);
        MaxLength(errors, "SiteId", request.SiteId, CodeMaxLength);
        MaxLength(errors, "SiteName", request.SiteName, NameMaxLength);
        MaxLength(errors, "Gateway", request.Gateway, CodeMaxLength);
        MaxLength(errors, "FirmwareVersion", request.FirmwareVersion, CodeMaxLength);

        // PowerSource is optional: when left out it comes from the model.
        OneOf(errors, "PowerSource", request.PowerSource, DevicePowerSource.All, required: false);
        OneOf(errors, "Health", request.Health, IoTDeviceHealth.All, required: false);

        // A newly added device has not reported a battery level yet, so it is optional.
        if (request.BatteryLevel is < 0 or > 100)
        {
            errors["BatteryLevel"] = ["BatteryLevel must be between 0 and 100"];
        }

        // Allows a little device clock drift, but not readings from the future.
        if (request.LastSeenAt?.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
        {
            errors["LastSeenAt"] = ["LastSeenAt cannot be in the future"];
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
