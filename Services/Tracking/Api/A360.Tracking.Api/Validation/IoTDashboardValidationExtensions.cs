using A360.Tracking.Api.Contracts;
using A360.Tracking.Domain.Entities;

namespace A360.Tracking.Api.Validation;

/// <summary>Validation for the IoT Tracking Dashboard records: alerts, utilization and telemetry readings.</summary>
public static class IoTDashboardValidationExtensions
{
    private const int CodeMaxLength = 50;
    private const int NameMaxLength = 100;
    private const int MessageMaxLength = 500;
    private const int MinutesPerDay = 1440;

    public static Dictionary<string, string[]> Validate(this CreateIoTAlertRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "AssetName", request.AssetName, NameMaxLength);
        RequireText(errors, "EventType", request.EventType, NameMaxLength);
        OptionalText(errors, "AssetRegistrationId", request.AssetRegistrationId, CodeMaxLength);
        OptionalText(errors, "DeviceId", request.DeviceId, CodeMaxLength);
        OptionalText(errors, "SiteName", request.SiteName, NameMaxLength);
        OptionalText(errors, "Message", request.Message, MessageMaxLength);
        RequireOption(errors, "Severity", request.Severity, IoTAlertSeverity.All);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            RequireOption(errors, "Status", request.Status, IoTAlertStatus.All);
        }

        NotInFuture(errors, "RaisedAt", request.RaisedAt);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateIoTAlertStatusRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        RequireOption(errors, "Status", request.Status, IoTAlertStatus.All);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(this CreateAssetUtilizationRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "AssetRegistrationId", request.AssetRegistrationId, CodeMaxLength);
        RequireText(errors, "AssetType", request.AssetType, NameMaxLength);
        OptionalText(errors, "AssetName", request.AssetName, NameMaxLength);
        OptionalText(errors, "DeviceId", request.DeviceId, CodeMaxLength);
        OptionalText(errors, "SiteName", request.SiteName, NameMaxLength);

        if (request.RunningMinutes is < 0 or > MinutesPerDay)
        {
            errors["RunningMinutes"] = [$"RunningMinutes must be between 0 and {MinutesPerDay}"];
        }

        if (request.IdleMinutes is < 0 or > MinutesPerDay)
        {
            errors["IdleMinutes"] = [$"IdleMinutes must be between 0 and {MinutesPerDay}"];
        }

        if ((request.RunningMinutes ?? 0) + (request.IdleMinutes ?? 0) > MinutesPerDay)
        {
            errors["IdleMinutes"] = [$"RunningMinutes + IdleMinutes cannot be more than {MinutesPerDay} (one day)"];
        }

        if (request.Date is not null && request.DateUtc > DateTime.UtcNow.Date)
        {
            errors["Date"] = ["Date cannot be in the future"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this CreateTelemetryReadingRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "DeviceId", request.DeviceId, CodeMaxLength);
        RequireText(errors, "Parameter", request.Parameter, NameMaxLength);
        OptionalText(errors, "SensorId", request.SensorId, CodeMaxLength);
        OptionalText(errors, "AssetRegistrationId", request.AssetRegistrationId, CodeMaxLength);
        OptionalText(errors, "Unit", request.Unit, 20);

        if (request.Value is null)
        {
            errors["Value"] = ["Value is required"];
        }

        NotInFuture(errors, "RecordedAt", request.RecordedAt);
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
            OptionalText(errors, field, value, maxLength);
        }
    }

    private static void OptionalText(Dictionary<string, string[]> errors, string field, string? value, int maxLength)
    {
        if (value?.Trim().Length > maxLength)
        {
            errors[field] = [$"{field} cannot be longer than {maxLength} characters"];
        }
    }

    private static void RequireOption(Dictionary<string, string[]> errors, string field, string? value, IReadOnlyList<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
        else if (ContractText.Canonical(value, allowed) is null)
        {
            errors[field] = [$"{field} must be one of: {string.Join(", ", allowed)}"];
        }
    }

    // Allows a little clock drift, but not times from the future.
    private static void NotInFuture(Dictionary<string, string[]> errors, string field, DateTime? value)
    {
        if (value?.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
        {
            errors[field] = [$"{field} cannot be in the future"];
        }
    }
}
