
using A360.Wip.Api.Contracts;

namespace A360.Wip.Api.Validation;

public static class IssueMasterValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateIssueMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.IssueId))
        {
            errors["IssueId"] =
                ["IssueId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.IssueType))
        {
            errors["IssueType"] =
                ["IssueType is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            errors["Description"] =
                ["Description is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateIssueMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.IssueType))
        {
            errors["IssueType"] =
                ["IssueType is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            errors["Description"] =
                ["Description is required"];
        }

        return errors;
    }
}
