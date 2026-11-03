using A360.Tracking.Api.Contracts;
using A360.Tracking.Domain.Entities;

namespace A360.Tracking.Api.Validation;

public static class LocationEventValidationExtensions
{
    private const int CodeMaxLength = 50;
    private const int NameMaxLength = 100;
    private const int DetailsMaxLength = 500;

    public static Dictionary<string, string[]> Validate(this CreateLocationEventRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "AssetId", request.AssetId, CodeMaxLength);
        RequireText(errors, "AssetName", request.AssetName, NameMaxLength);
        RequireText(errors, "ZoneName", request.ZoneName, NameMaxLength);

        if (string.IsNullOrWhiteSpace(request.EventType))
        {
            errors["EventType"] = ["EventType is required"];
        }
        else if (ContractText.Canonical(request.EventType, LocationEventType.All) is null)
        {
            errors["EventType"] = [$"EventType must be one of: {string.Join(", ", LocationEventType.All)}"];
        }

        if (request.Details?.Trim().Length > DetailsMaxLength)
        {
            errors["Details"] = [$"Details cannot be longer than {DetailsMaxLength} characters"];
        }

        // Allows a little clock drift, but not events from the future.
        if (request.EventTime?.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
        {
            errors["EventTime"] = ["EventTime cannot be in the future"];
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
