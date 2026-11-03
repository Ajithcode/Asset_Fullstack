
using A360.Wip.Api.Contracts;

namespace A360.Wip.Api.Validation;

public static class LocationMasterValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateLocationMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.LocationId))
        {
            errors["LocationId"] =
                ["LocationId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Site))
        {
            errors["Site"] =
                ["Site is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateLocationMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Site))
        {
            errors["Site"] =
                ["Site is required"];
        }

        return errors;
    }
}
