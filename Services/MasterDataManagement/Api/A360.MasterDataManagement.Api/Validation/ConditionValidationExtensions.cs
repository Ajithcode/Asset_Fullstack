using System.Text.RegularExpressions;
using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class ConditionValidationExtensions
{
    // "#RGB" or "#RRGGBB".
    private static readonly Regex ColorPattern = new(
        "^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$",
        RegexOptions.Compiled);

    public static Dictionary<string, string[]> Validate(
        this CreateConditionRequest request)
    {
        return ValidateFields(request.ConditionName, request.Code, request.Color, request.HealthScore, request.DisplayOrder);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateConditionRequest request)
    {
        return ValidateFields(request.ConditionName, request.Code, request.Color, request.HealthScore, request.DisplayOrder);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateConditionStatusRequest request)
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
        string? conditionName,
        string? code,
        string? color,
        int? healthScore,
        int? displayOrder)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(conditionName))
        {
            errors["ConditionName"] =
                ["ConditionName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (!string.IsNullOrWhiteSpace(color) && !ColorPattern.IsMatch(color.Trim()))
        {
            errors["Color"] =
                ["Color must be a hex colour such as #2E7D32"];
        }

        if (healthScore is < 0 or > 100)
        {
            errors["HealthScore"] =
                ["HealthScore must be between 0 and 100"];
        }

        if (displayOrder is < 1)
        {
            errors["DisplayOrder"] =
                ["DisplayOrder must be 1 or more"];
        }

        return errors;
    }
}
