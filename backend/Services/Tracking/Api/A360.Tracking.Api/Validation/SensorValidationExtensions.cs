using A360.Tracking.Api.Contracts;
using A360.Tracking.Domain.Entities;

namespace A360.Tracking.Api.Validation;

public static class SensorValidationExtensions
{
    private const int CodeMaxLength = 50;
    private const int NameMaxLength = 100;
    private const int UnitMaxLength = 20;

    public static Dictionary<string, string[]> Validate(this CreateSensorRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "SensorId", request.SensorId, CodeMaxLength);
        RequireText(errors, "Name", request.Name, NameMaxLength);
        RequireText(errors, "SensorType", request.SensorType, NameMaxLength);
        RequireText(errors, "MeasuredParameter", request.MeasuredParameter, NameMaxLength);

        MaxLength(errors, "Unit", request.Unit, UnitMaxLength);
        MaxLength(errors, "DeviceId", request.DeviceId, CodeMaxLength);
        MaxLength(errors, "DeviceModel", request.DeviceModel, NameMaxLength);
        MaxLength(errors, "AssetRegistrationId", request.AssetRegistrationId, CodeMaxLength);
        MaxLength(errors, "AssetName", request.AssetName, NameMaxLength);
        MaxLength(errors, "MeasurementPoint", request.MeasurementPoint, NameMaxLength);

        if (!string.IsNullOrWhiteSpace(request.Status) && ContractText.Canonical(request.Status, SensorStatus.All) is null)
        {
            errors["Status"] = [$"Status must be one of: {string.Join(", ", SensorStatus.All)}"];
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

    private static void MaxLength(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (value?.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} cannot be longer than {maxLength} characters"];
        }
    }
}
