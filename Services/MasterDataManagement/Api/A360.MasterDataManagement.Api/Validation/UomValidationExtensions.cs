using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class UomValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateUomRequest request)
    {
        return ValidateFields(request.UomName, request.Code, request.Type, request.Symbol, request.ConversionFactor);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateUomRequest request)
    {
        return ValidateFields(request.UomName, request.Code, request.Type, request.Symbol, request.ConversionFactor);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateUomStatusRequest request)
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
        string? uomName,
        string? code,
        string? type,
        string? symbol,
        double? conversionFactor)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(uomName))
        {
            errors["UomName"] =
                ["UomName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            errors["Type"] =
                ["Type is required"];
        }

        if (string.IsNullOrWhiteSpace(symbol))
        {
            errors["Symbol"] =
                ["Symbol is required"];
        }

        if (conversionFactor <= 0)
        {
            errors["ConversionFactor"] =
                ["ConversionFactor must be greater than 0"];
        }

        return errors;
    }
}
