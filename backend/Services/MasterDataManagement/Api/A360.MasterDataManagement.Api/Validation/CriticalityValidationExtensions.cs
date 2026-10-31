using System.Text.RegularExpressions;
using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class CriticalityValidationExtensions
{
    // "#RGB" or "#RRGGBB".
    private static readonly Regex ColorPattern = new(
        "^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$",
        RegexOptions.Compiled);

    public static Dictionary<string, string[]> Validate(
        this CreateCriticalityRequest request)
    {
        return ValidateFields(request.CriticalityName, request.Code, request.Color, request.ResponseTimeHours);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateCriticalityRequest request)
    {
        return ValidateFields(request.CriticalityName, request.Code, request.Color, request.ResponseTimeHours);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateCriticalityStatusRequest request)
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
        string? criticalityName,
        string? code,
        string? color,
        int? responseTimeHours)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(criticalityName))
        {
            errors["CriticalityName"] =
                ["CriticalityName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (!string.IsNullOrWhiteSpace(color) && !ColorPattern.IsMatch(color.Trim()))
        {
            errors["Color"] =
                ["Color must be a hex colour such as #C62828"];
        }

        if (responseTimeHours is < 1 or > 720)
        {
            errors["ResponseTimeHours"] =
                ["ResponseTimeHours must be between 1 and 720"];
        }

        return errors;
    }
}
