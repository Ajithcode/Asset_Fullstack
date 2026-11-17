using A360.Tracking.Api.Contracts;
using A360.Tracking.Domain.Entities;

namespace A360.Tracking.Api.Validation;

public static class DeviceModelValidationExtensions
{
    private const int CodeMaxLength = 50;
    private const int NameMaxLength = 100;
    private const int DescriptionMaxLength = 500;
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
        KnownItems(errors, "SupportedMeasurements", request.SupportedMeasurements, DeviceMeasurement.All.Select(m => m.Name).ToList());
        KnownItems(errors, "ConfigurableAttributes", request.ConfigurableAttributes, DeviceModelAttribute.All);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this CreateDeviceTypeRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "Name", request.Name, NameMaxLength);
        OneOf(errors, "Icon", request.Icon, DeviceTypeIcon.All, required: false);
        OneOf(errors, "ThumbnailStyle", request.ThumbnailStyle, DeviceTypeThumbnail.All, required: false);
        MaxLength(errors, "Description", request.Description, DescriptionMaxLength);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this DeviceModelListQuery query)
    {
        var errors = new Dictionary<string, string[]>();

        OneOf(errors, "connectivity", query.Connectivity, DeviceModelConnectivity.All, required: false);
        OneOf(errors, "status", query.Status, DeviceModelStatus.All, required: false);

        if (query.Page is < 1)
        {
            errors["page"] = ["page must be 1 or more"];
        }

        if (query.PageSize is < 1 or > 200)
        {
            errors["pageSize"] = ["pageSize must be between 1 and 200"];
        }

        return errors;
    }

    /// <summary>An array whose items must each be one of the listed options (ignoring case).</summary>
    private static void KnownItems(Dictionary<string, string[]> errors, string field, IReadOnlyList<string>? values, IReadOnlyList<string> allowed)
    {
        if (values?.Count > MaxListItems)
        {
            errors[field] = [$"At most {MaxListItems} {field} are allowed"];
            return;
        }

        var unknown = (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value) && ContractText.Canonical(value, allowed) is null)
            .Select(value => value.Trim())
            .ToList();

        if (unknown.Count > 0)
        {
            errors[field] = [$"Unknown {field}: {string.Join(", ", unknown)}. Allowed: {string.Join(", ", allowed)}"];
        }
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
