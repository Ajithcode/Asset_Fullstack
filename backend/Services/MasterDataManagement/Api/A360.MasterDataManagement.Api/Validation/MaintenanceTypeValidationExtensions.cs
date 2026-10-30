using System.Text.RegularExpressions;
using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class MaintenanceTypeValidationExtensions
{
    // "#RGB" or "#RRGGBB".
    private static readonly Regex ColorPattern = new(
        "^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$",
        RegexOptions.Compiled);

    public static Dictionary<string, string[]> Validate(
        this CreateMaintenanceTypeRequest request)
    {
        return ValidateFields(request.MaintenanceTypeName, request.Code, request.Description, request.Color);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateMaintenanceTypeRequest request)
    {
        return ValidateFields(request.MaintenanceTypeName, request.Code, request.Description, request.Color);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateMaintenanceTypeStatusRequest request)
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
        string? maintenanceTypeName,
        string? code,
        string? description,
        string? color)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(maintenanceTypeName))
        {
            errors["MaintenanceTypeName"] =
                ["MaintenanceTypeName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            errors["Description"] =
                ["Description is required"];
        }

        if (!string.IsNullOrWhiteSpace(color) && !ColorPattern.IsMatch(color.Trim()))
        {
            errors["Color"] =
                ["Color must be a hex colour such as #1565C0"];
        }

        return errors;
    }
}
