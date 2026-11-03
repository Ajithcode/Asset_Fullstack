
using A360.Wip.Api.Contracts;

namespace A360.Wip.Api.Validation;

public static class SlaMasterValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateSlaMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.SlaId))
        {
            errors["SlaId"] =
                ["SlaId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.SlaName))
        {
            errors["SlaName"] =
                ["SlaName is required"];
        }

        if (request.ResponseTimeMinutes is < 0)
        {
            errors["ResponseTimeMinutes"] =
                ["ResponseTimeMinutes must not be negative"];
        }

        if (request.ResolutionTimeMinutes is < 0)
        {
            errors["ResolutionTimeMinutes"] =
                ["ResolutionTimeMinutes must not be negative"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateSlaMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.SlaName))
        {
            errors["SlaName"] =
                ["SlaName is required"];
        }

        if (request.ResponseTimeMinutes is < 0)
        {
            errors["ResponseTimeMinutes"] =
                ["ResponseTimeMinutes must not be negative"];
        }

        if (request.ResolutionTimeMinutes is < 0)
        {
            errors["ResolutionTimeMinutes"] =
                ["ResolutionTimeMinutes must not be negative"];
        }

        return errors;
    }
}
