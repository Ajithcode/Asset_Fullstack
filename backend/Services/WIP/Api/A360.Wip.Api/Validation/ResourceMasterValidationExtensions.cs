
using A360.Wip.Api.Contracts;

namespace A360.Wip.Api.Validation;

public static class ResourceMasterValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateResourceMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.ResourceId))
        {
            errors["ResourceId"] =
                ["ResourceId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.ResourceName))
        {
            errors["ResourceName"] =
                ["ResourceName is required"];
        }

        if (!string.IsNullOrWhiteSpace(request.Email) &&
            !request.Email.Contains('@'))
        {
            errors["Email"] =
                ["Email must be a valid email address"];
        }

        if (request.CostPerHour is < 0)
        {
            errors["CostPerHour"] =
                ["CostPerHour must not be negative"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateResourceMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.ResourceName))
        {
            errors["ResourceName"] =
                ["ResourceName is required"];
        }

        if (!string.IsNullOrWhiteSpace(request.Email) &&
            !request.Email.Contains('@'))
        {
            errors["Email"] =
                ["Email must be a valid email address"];
        }

        if (request.CostPerHour is < 0)
        {
            errors["CostPerHour"] =
                ["CostPerHour must not be negative"];
        }

        return errors;
    }
}
