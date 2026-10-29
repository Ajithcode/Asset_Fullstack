using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class RegulationValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateRegulationRequest request)
    {
        return ValidateFields(request.RegulationName, request.Code, request.Country, request.Category);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateRegulationRequest request)
    {
        return ValidateFields(request.RegulationName, request.Code, request.Country, request.Category);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateRegulationStatusRequest request)
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
        string? regulationName,
        string? code,
        string? country,
        string? category)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(regulationName))
        {
            errors["RegulationName"] =
                ["RegulationName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            errors["Country"] =
                ["Country is required"];
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            errors["Category"] =
                ["Category is required"];
        }

        return errors;
    }
}
