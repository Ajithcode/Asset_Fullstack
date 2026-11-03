using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class ModuleAccessValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateModuleAccessRequest request)
    {
        return ValidateFields(request.ModuleName, request.Code, request.Category);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateModuleAccessRequest request)
    {
        return ValidateFields(request.ModuleName, request.Code, request.Category);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateModuleAccessStatusRequest request)
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
        string? moduleName,
        string? code,
        string? category)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(moduleName))
        {
            errors["ModuleName"] =
                ["ModuleName is required"];
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

        return errors;
    }
}
