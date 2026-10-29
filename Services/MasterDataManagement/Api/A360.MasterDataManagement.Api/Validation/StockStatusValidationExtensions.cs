using System.Text.RegularExpressions;
using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class StockStatusValidationExtensions
{
    // "#RGB" or "#RRGGBB".
    private static readonly Regex ColorPattern = new(
        "^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$",
        RegexOptions.Compiled);

    public static Dictionary<string, string[]> Validate(
        this CreateStockStatusRequest request)
    {
        return ValidateFields(request.StatusName, request.Code, request.Type, request.Color);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateStockStatusRequest request)
    {
        return ValidateFields(request.StatusName, request.Code, request.Type, request.Color);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateStockStatusStatusRequest request)
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
        string? statusName,
        string? code,
        string? type,
        string? color)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(statusName))
        {
            errors["StatusName"] =
                ["StatusName is required"];
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

        if (!string.IsNullOrWhiteSpace(color) && !ColorPattern.IsMatch(color.Trim()))
        {
            errors["Color"] =
                ["Color must be a hex colour such as #2E7D32"];
        }

        return errors;
    }
}
