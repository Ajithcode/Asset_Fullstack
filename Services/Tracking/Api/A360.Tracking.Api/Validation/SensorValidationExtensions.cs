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

        if (string.IsNullOrWhiteSpace(request.SensorId) && string.IsNullOrWhiteSpace(request.SensorNumber))
        {
            errors["SensorNumber"] = ["SensorNumber (or SensorId) is required"];
        }
        else
        {
            MaxLength(errors, "SensorId", request.ResolvedSensorId(), CodeMaxLength);
        }

        RequireText(errors, "SensorType", request.SensorType, NameMaxLength);
        RequireText(errors, "SerialNumber", request.SerialNumber, CodeMaxLength);
        RequireText(errors, "DeviceId", request.DeviceId, CodeMaxLength);
        RequireText(errors, "MeasurementPoint", request.MeasurementPoint, NameMaxLength);

        MaxLength(errors, "Name", request.Name, NameMaxLength);
        MaxLength(errors, "MeasuredParameter", request.MeasuredParameter, NameMaxLength);
        MaxLength(errors, "SensorNumber", request.SensorNumber, CodeMaxLength);
        MaxLength(errors, "AssetCode", request.AssetCode, CodeMaxLength);
        MaxLength(errors, "SiteId", request.SiteId, CodeMaxLength);
        MaxLength(errors, "SiteName", request.SiteName, NameMaxLength);

        if (string.IsNullOrWhiteSpace(request.AssetRegistrationId)
            && string.IsNullOrWhiteSpace(request.AssetCode)
            && string.IsNullOrWhiteSpace(request.AssetName))
        {
            errors["Asset"] = ["Asset is required: send AssetRegistrationId, AssetCode or AssetName"];
        }

        if (request.CalibrationDueDate is null)
        {
            errors["CalibrationDueDate"] = ["CalibrationDueDate is required"];
        }

        if (request.Channel is < 1)
        {
            errors["Channel"] = ["Channel must be 1 or more"];
        }

        if (request.SamplingIntervalSeconds is { } interval && !TelemetrySamplingInterval.Seconds.Contains(interval))
        {
            errors["SamplingIntervalSeconds"] = [$"SamplingIntervalSeconds must be one of: {string.Join(", ", TelemetrySamplingInterval.Seconds)}"];
        }

        if (request.LowAlarm is { } low && request.HighAlarm is { } high && low >= high)
        {
            errors["HighAlarm"] = ["HighAlarm must be greater than LowAlarm"];
        }

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
