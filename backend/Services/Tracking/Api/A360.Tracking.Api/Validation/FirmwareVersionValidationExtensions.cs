using A360.Tracking.Api.Contracts;
using A360.Tracking.Domain.Entities;

namespace A360.Tracking.Api.Validation;

public static class FirmwareVersionValidationExtensions
{
    private const int NameMaxLength = 100;
    private const int VersionMaxLength = 50;

    public static Dictionary<string, string[]> Validate(this CreateFirmwareVersionRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "DeviceModel", request.DeviceModel, NameMaxLength);
        RequireText(errors, "CurrentVersion", request.CurrentVersion, VersionMaxLength);
        RequireText(errors, "LatestVersion", request.LatestVersion, VersionMaxLength);

        if (request.DeviceCount < 0)
        {
            errors["DeviceCount"] = ["DeviceCount cannot be negative"];
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && ContractText.Canonical(request.Status, FirmwareUpdateStatus.All) is null)
        {
            errors["Status"] = [$"Status must be one of: {string.Join(", ", FirmwareUpdateStatus.All)}"];
        }

        return errors;
    }

    private static void RequireText(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
        else if (value.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} cannot be longer than {maxLength} characters"];
        }
    }
}
