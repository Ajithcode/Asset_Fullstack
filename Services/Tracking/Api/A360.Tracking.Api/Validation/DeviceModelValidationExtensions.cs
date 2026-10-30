using A360.Tracking.Api.Contracts;
using A360.Tracking.Domain.Entities;

namespace A360.Tracking.Api.Validation;

public static class DeviceModelValidationExtensions
{
    private const int CodeMaxLength = 50;
    private const int NameMaxLength = 100;
    private const int MaxMeasurements = 30;
    private const int MaxListItems = 30;

    public static Dictionary<string, string[]> Validate(this CreateDeviceModelRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "ModelNumber", request.ModelNumber, CodeMaxLength);
        RequireText(errors, "Manufacturer", request.Manufacturer, NameMaxLength);
        RequireText(errors, "DeviceType", request.DeviceType, NameMaxLength);

        OneOf(errors, "Connectivity", request.Connectivity, DeviceModelConnectivity.All, required: true);
        OneOf(errors, "PowerSource", request.PowerSource, DeviceModelPowerSource.All, required: true);
        OneOf(errors, "Status", request.Status, DeviceModelStatus.All, required: false);

        MaxLength(errors, "Protocol", request.Protocol, CodeMaxLength);
        MaxLength(errors, "IpRating", request.IpRating, CodeMaxLength);
        MaxLength(errors, "BatteryLife", request.BatteryLife, NameMaxLength);
        MaxLength(errors, "OperatingTemperature", request.OperatingTemperature, NameMaxLength);
        MaxLength(errors, "FirmwareFrom", request.FirmwareFrom, CodeMaxLength);
        MaxLength(errors, "FirmwareTo", request.FirmwareTo, CodeMaxLength);
        ListItems(errors, "FrequencyBands", request.FrequencyBands, CodeMaxLength);
        ListItems(errors, "ConfigurableAttributes", request.ConfigurableAttributes, NameMaxLength);

        if (request.SupportedMeasurements?.Count > MaxMeasurements)
        {
            errors["SupportedMeasurements"] = [$"At most {MaxMeasurements} supported measurements are allowed"];
        }
        else if (request.SupportedMeasurements?.Any(measurement => measurement?.Trim().Length > NameMaxLength) == true)
        {
            errors["SupportedMeasurements"] = [$"Each supported measurement cannot be longer than {NameMaxLength} characters"];
        }

        return errors;
    }

    private static void ListItems(Dictionary<string, string[]> errors, string field, IReadOnlyList<string>? values, int maxLength)
    {
        if (values?.Count > MaxListItems)
        {
            errors[field] = [$"At most {MaxListItems} {field} are allowed"];
        }
        else if (values?.Any(value => value?.Trim().Length > maxLength) == true)
        {
            errors[field] = [$"Each of {field} cannot be longer than {maxLength} characters"];
        }
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
