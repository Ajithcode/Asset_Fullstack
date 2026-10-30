using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class UpdateSourceValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateUpdateSourceRequest request)
    {
        return ValidateFields(request.SourceName, request.Code, request.Type);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateUpdateSourceRequest request)
    {
        return ValidateFields(request.SourceName, request.Code, request.Type);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateUpdateSourceStatusRequest request)
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
        string? sourceName,
        string? code,
        string? type)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(sourceName))
        {
            errors["SourceName"] =
                ["SourceName is required"];
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

        return errors;
    }
}
