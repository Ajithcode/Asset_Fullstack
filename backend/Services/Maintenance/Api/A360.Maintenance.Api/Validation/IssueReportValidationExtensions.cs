
using A360.Maintenance.Api.Contracts;

namespace A360.Maintenance.Api.Validation;

public static class IssueReportValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateIssueReportRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.IssueId))
        {
            errors["IssueId"] =
                ["IssueId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.IssueType))
        {
            errors["IssueType"] =
                ["IssueType is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Severity))
        {
            errors["Severity"] =
                ["Severity is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateIssueReportRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.IssueId))
        {
            errors["IssueId"] =
                ["IssueId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.IssueType))
        {
            errors["IssueType"] =
                ["IssueType is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Severity))
        {
            errors["Severity"] =
                ["Severity is required"];
        }

        return errors;
    }
}
