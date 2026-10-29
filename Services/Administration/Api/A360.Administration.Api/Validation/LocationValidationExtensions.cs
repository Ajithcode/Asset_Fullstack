using A360.Administration.Api.Contracts;

namespace A360.Administration.Api.Validation;

public static class LocationValidationExtensions
{
    // The root of the hierarchy is the only location allowed without a parent.
    public const string RootLocationType = "Organization";

    private static readonly string[] AllowedStatuses = ["Active", "Inactive"];

    public static Dictionary<string, string[]> Validate(this CreateLocationRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, nameof(request.LocationCode), request.LocationCode);
        Require(errors, nameof(request.LocationName), request.LocationName);
        Require(errors, nameof(request.LocationType), request.LocationType);
        Require(errors, nameof(request.Country), request.Country);
        Require(errors, nameof(request.City), request.City);
        Require(errors, nameof(request.TimeZone), request.TimeZone);
        Require(errors, nameof(request.LocationManager), request.LocationManager);
        Require(errors, nameof(request.Status), request.Status);

        var isRoot = string.Equals(request.LocationType?.Trim(), RootLocationType, StringComparison.OrdinalIgnoreCase);
        if (!isRoot && string.IsNullOrWhiteSpace(request.ParentLocationId))
        {
            errors[nameof(request.ParentLocationId)] = ["ParentLocationId is required"];
        }

        if (!string.IsNullOrWhiteSpace(request.Status)
            && !AllowedStatuses.Contains(request.Status.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            errors[nameof(request.Status)] = [$"Status must be one of: {string.Join(", ", AllowedStatuses)}"];
        }

        return errors;
    }

    private static void Require(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
    }
}
