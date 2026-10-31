using A360.Administration.Api.Contracts;

namespace A360.Administration.Api.Validation;

public static class LocationValidationExtensions
{
    // The root of the hierarchy is the only location allowed without a parent.
    public const string RootLocationType = "Organization";

    private static readonly string[] AllowedStatuses = ["Active", "Inactive"];

    public static Dictionary<string, string[]> Validate(this CreateLocationRequest request)
    {
        return ValidateLocation(
            request.LocationCode,
            request.LocationName,
            request.LocationType,
            request.ParentLocationId,
            request.Country,
            request.City,
            request.TimeZone,
            request.LocationManager,
            request.Status);
    }

    public static Dictionary<string, string[]> Validate(this UpdateLocationRequest request)
    {
        return ValidateLocation(
            request.LocationCode,
            request.LocationName,
            request.LocationType,
            request.ParentLocationId,
            request.Country,
            request.City,
            request.TimeZone,
            request.LocationManager,
            request.Status);
    }

    private static Dictionary<string, string[]> ValidateLocation(
        string? locationCode,
        string? locationName,
        string? locationType,
        string? parentLocationId,
        string? country,
        string? city,
        string? timeZone,
        string? locationManager,
        string? status)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, "LocationCode", locationCode);
        Require(errors, "LocationName", locationName);
        Require(errors, "LocationType", locationType);
        Require(errors, "Country", country);
        Require(errors, "City", city);
        Require(errors, "TimeZone", timeZone);
        Require(errors, "LocationManager", locationManager);
        Require(errors, "Status", status);

        var isRoot = string.Equals(locationType?.Trim(), RootLocationType, StringComparison.OrdinalIgnoreCase);
        if (!isRoot && string.IsNullOrWhiteSpace(parentLocationId))
        {
            errors["ParentLocationId"] = ["ParentLocationId is required"];
        }

        if (!string.IsNullOrWhiteSpace(status)
            && !AllowedStatuses.Contains(status.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            errors["Status"] = [$"Status must be one of: {string.Join(", ", AllowedStatuses)}"];
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
