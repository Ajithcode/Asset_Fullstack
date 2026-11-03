using A360.Tracking.Api.Contracts;
using A360.Tracking.Domain.Entities;

namespace A360.Tracking.Api.Validation;

public static class DeviceMaintenanceValidationExtensions
{
    private const int CodeMaxLength = 50;
    private const int NameMaxLength = 100;

    public static Dictionary<string, string[]> Validate(this CreateDeviceMaintenanceRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "DeviceId", request.DeviceId, CodeMaxLength);
        RequireText(errors, "Model", request.Model, NameMaxLength);
        RequireText(errors, "SiteName", request.SiteName, NameMaxLength);

        MaxLength(errors, "DeviceName", request.DeviceName, NameMaxLength);
        MaxLength(errors, "AssetRegistrationId", request.AssetRegistrationId, CodeMaxLength);
        MaxLength(errors, "AssetName", request.AssetName, NameMaxLength);
        MaxLength(errors, "AssignedTo", request.AssignedTo, NameMaxLength);

        OneOf(errors, "MaintenanceType", request.MaintenanceType, DeviceMaintenanceType.All, required: true);
        OneOf(errors, "Status", request.Status, DeviceMaintenanceStatus.All, required: false);

        if (request.DueDate is null)
        {
            errors["DueDate"] = ["DueDate is required"];
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
