using A360.Tracking.Api.Contracts;
using A360.Tracking.Domain.Entities;

namespace A360.Tracking.Api.Validation;

public static class IoTDeviceValidationExtensions
{
    private const int CodeMaxLength = 50;
    private const int NameMaxLength = 100;

    public static Dictionary<string, string[]> Validate(this AddIoTDeviceRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "DeviceType", request.DeviceType, NameMaxLength);
        RequireText(errors, "Model", request.Model, NameMaxLength);
        RequireText(errors, "DeviceName", request.DeviceName, NameMaxLength);
        RequireText(errors, "DeviceId", request.DeviceId, CodeMaxLength);
        RequireText(errors, "SerialNumber", request.SerialNumber, CodeMaxLength);
        RequireText(errors, "Connectivity", request.Connectivity, CodeMaxLength);

        MaxLength(errors, "SiteId", request.SiteId, CodeMaxLength);
        MaxLength(errors, "SiteName", request.SiteName, NameMaxLength);
        MaxLength(errors, "Gateway", request.Gateway, CodeMaxLength);
        MaxLength(errors, "AssetCode", request.AssetCode, CodeMaxLength);
        MaxLength(errors, "FirmwareVersion", request.FirmwareVersion, CodeMaxLength);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this IoTDeviceListQuery query)
    {
        var errors = new Dictionary<string, string[]>();

        OneOf(errors, "status", query.Status, DeviceConnection.All);
        OneOf(errors, "health", query.Health, IoTDeviceHealth.All);

        if (query.Page is < 1)
        {
            errors["page"] = ["page must be 1 or more"];
        }

        if (query.PageSize is < 1 or > 200)
        {
            errors["pageSize"] = ["pageSize must be between 1 and 200"];
        }

        if (query.Limit is < 1 or > 100)
        {
            errors["limit"] = ["limit must be between 1 and 100"];
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

    private static void OneOf(Dictionary<string, string[]> errors, string field, string? value, IReadOnlyList<string> allowed)
    {
        if (!string.IsNullOrWhiteSpace(value) && ContractText.Canonical(value, allowed) is null)
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
