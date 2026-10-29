using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class StandardValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateStandardRequest request)
    {
        return ValidateFields(request.StandardName, request.Code, request.Organization, request.Category);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateStandardRequest request)
    {
        return ValidateFields(request.StandardName, request.Code, request.Organization, request.Category);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateStandardStatusRequest request)
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
        string? standardName,
        string? code,
        string? organization,
        string? category)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(standardName))
        {
            errors["StandardName"] =
                ["StandardName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (string.IsNullOrWhiteSpace(organization))
        {
            errors["Organization"] =
                ["Organization is required"];
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            errors["Category"] =
                ["Category is required"];
        }

        return errors;
    }
}
