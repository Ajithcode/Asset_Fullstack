using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class PermitTypeValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreatePermitTypeRequest request)
    {
        return ValidateFields(request.PermitTypeName, request.Code, request.Category, request.ValidTillDays);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdatePermitTypeRequest request)
    {
        return ValidateFields(request.PermitTypeName, request.Code, request.Category, request.ValidTillDays);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdatePermitTypeStatusRequest request)
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
        string? permitTypeName,
        string? code,
        string? category,
        int? validTillDays)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(permitTypeName))
        {
            errors["PermitTypeName"] =
                ["PermitTypeName is required"];
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

        if (validTillDays < 1)
        {
            errors["ValidTillDays"] =
                ["ValidTillDays must be at least 1"];
        }

        return errors;
    }
}
