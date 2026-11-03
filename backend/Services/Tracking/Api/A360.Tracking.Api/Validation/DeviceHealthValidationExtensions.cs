using A360.Tracking.Api.Contracts;
using A360.Tracking.Domain.Entities;

namespace A360.Tracking.Api.Validation;

public static class DeviceHealthValidationExtensions
{
    private const int CodeMaxLength = 50;
    private const int NameMaxLength = 100;

    // RSSI readings outside this range are not real signal values.
    private const int MinSignalDbm = -150;
    private const int MaxSignalDbm = 0;

    public static Dictionary<string, string[]> Validate(this CreateDeviceHealthRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "DeviceId", request.DeviceId, CodeMaxLength);
        RequireText(errors, "DeviceModel", request.DeviceModel, NameMaxLength);

        MaxLength(errors, "AssetRegistrationId", request.AssetRegistrationId, CodeMaxLength);
        MaxLength(errors, "AssetName", request.AssetName, NameMaxLength);
        MaxLength(errors, "SiteName", request.SiteName, NameMaxLength);
        MaxLength(errors, "Gateway", request.Gateway, CodeMaxLength);

        OneOf(errors, "Connectivity", request.Connectivity, DeviceConnectivity.All, required: true);
        OneOf(errors, "SignalQuality", request.SignalQuality, SignalQuality.All, required: false);
        OneOf(errors, "PowerSource", request.PowerSource, DevicePowerSource.All, required: true);
        OneOf(errors, "Health", request.Health, DeviceHealthStatus.All, required: true);

        if (request.SignalStrengthDbm is < MinSignalDbm or > MaxSignalDbm)
        {
            errors["SignalStrengthDbm"] = [$"SignalStrengthDbm must be between {MinSignalDbm} and {MaxSignalDbm}"];
        }

        var isBattery = string.Equals(request.PowerSource?.Trim(), DevicePowerSource.Battery, StringComparison.OrdinalIgnoreCase);
        if (isBattery && request.BatteryLevel is null)
        {
            errors["BatteryLevel"] = ["BatteryLevel is required when PowerSource is Battery"];
        }
        else if (request.BatteryLevel is < 0 or > 100)
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
