using A360.Tracking.Api.Contracts;
using A360.Tracking.Domain.Entities;

namespace A360.Tracking.Api.Validation;

public static class GeofenceValidationExtensions
{
    private const int NameMaxLength = 100;

    public static Dictionary<string, string[]> Validate(this CreateGeofenceRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireText(errors, "Name", request.Name, NameMaxLength);
        RequireText(errors, "SiteName", request.SiteName, NameMaxLength);

        if (string.IsNullOrWhiteSpace(request.GeofenceType))
        {
            errors["GeofenceType"] = ["GeofenceType is required"];
        }
        else if (ContractText.Canonical(request.GeofenceType, GeofenceType.All) is null)
        {
            errors["GeofenceType"] = [$"GeofenceType must be one of: {string.Join(", ", GeofenceType.All)}"];
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && ContractText.Canonical(request.Status, GeofenceStatus.All) is null)
        {
            errors["Status"] = [$"Status must be one of: {string.Join(", ", GeofenceStatus.All)}"];
        }

        if (request.MonitoredAssetCount < 0)
        {
            errors["MonitoredAssetCount"] = ["MonitoredAssetCount cannot be negative"];
        }

        if (request.RuleCount < 0)
        {
            errors["RuleCount"] = ["RuleCount cannot be negative"];
        }

        // Allows a little clock drift, but not events from the future.
        if (request.LastEventAt?.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
        {
            errors["LastEventAt"] = ["LastEventAt cannot be in the future"];
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
