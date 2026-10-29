using System.Text.RegularExpressions;
using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class PrioritySeverityValidationExtensions
{
    // "#RGB" or "#RRGGBB".
    private static readonly Regex ColorPattern = new(
        "^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$",
        RegexOptions.Compiled);

    public static Dictionary<string, string[]> Validate(
        this CreatePrioritySeverityRequest request)
    {
        return ValidateFields(request.Name, request.Code, request.Type, request.Color);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdatePrioritySeverityRequest request)
    {
        return ValidateFields(request.Name, request.Code, request.Type, request.Color);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdatePrioritySeverityStatusRequest request)
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
        string? name,
        string? code,
        string? type,
        string? color)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors["Name"] =
                ["Name is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (PrioritySeverityTypes.Normalize(type) is null)
        {
            errors["Type"] =
                [$"Type must be one of: {string.Join(", ", PrioritySeverityTypes.All)}"];
        }

        if (!string.IsNullOrWhiteSpace(color) && !ColorPattern.IsMatch(color.Trim()))
        {
            errors["Color"] =
                ["Color must be a hex colour such as #1565C0"];
        }

        return errors;
    }
}
