using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class FailureCodeValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateFailureCodeRequest request)
    {
        return ValidateFields(request.FailureCodeName, request.Code, request.Category, request.Severity);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateFailureCodeRequest request)
    {
        return ValidateFields(request.FailureCodeName, request.Code, request.Category, request.Severity);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateFailureCodeStatusRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            errors["Status"] =
                ["Status is required"];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateFields(
        string? failureCodeName,
        string? code,
        string? category,
        string? severity)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(failureCodeName))
        {
            errors["FailureCodeName"] =
                ["FailureCodeName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            errors["Category"] =
                ["Category is required"];
        }

        if (FailureCodeSeverities.Normalize(severity) is null)
        {
            errors["Severity"] =
                [$"Severity must be one of: {string.Join(", ", FailureCodeSeverities.All)}"];
        }

        return errors;
    }
}
