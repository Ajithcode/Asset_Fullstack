
using A360.Wip.Api.Contracts;

namespace A360.Wip.Api.Validation;

public static class JobMasterValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateJobMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.JobId))
        {
            errors["JobId"] =
                ["JobId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.JobName))
        {
            errors["JobName"] =
                ["JobName is required"];
        }

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        if (request.ProgressPercentage is < 0 or > 100)
        {
            errors["ProgressPercentage"] =
                ["ProgressPercentage must be between 0 and 100"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateJobMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.JobName))
        {
            errors["JobName"] =
                ["JobName is required"];
        }

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        if (request.ProgressPercentage is < 0 or > 100)
        {
            errors["ProgressPercentage"] =
                ["ProgressPercentage must be between 0 and 100"];
        }

        return errors;
    }
}
