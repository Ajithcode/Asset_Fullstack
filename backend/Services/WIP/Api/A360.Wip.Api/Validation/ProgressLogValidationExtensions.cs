
using A360.Wip.Api.Contracts;

namespace A360.Wip.Api.Validation;

public static class ProgressLogValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateProgressLogRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.LogId))
        {
            errors["LogId"] =
                ["LogId is required"];
        }

        if (request.ProgressPercentage is < 0 or > 100)
        {
            errors["ProgressPercentage"] =
                ["ProgressPercentage must be between 0 and 100"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateProgressLogRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.ProgressPercentage is < 0 or > 100)
        {
            errors["ProgressPercentage"] =
                ["ProgressPercentage must be between 0 and 100"];
        }

        return errors;
    }
}
