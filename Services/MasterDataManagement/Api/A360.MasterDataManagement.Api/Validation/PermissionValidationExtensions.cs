using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class PermissionValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreatePermissionRequest request)
    {
        return ValidateFields(request.PermissionName, request.Code, request.Module, request.Type);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdatePermissionRequest request)
    {
        return ValidateFields(request.PermissionName, request.Code, request.Module, request.Type);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdatePermissionStatusRequest request)
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
        string? permissionName,
        string? code,
        MasterDataReferenceDto? module,
        string? type)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(permissionName))
        {
            errors["PermissionName"] =
                ["PermissionName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (string.IsNullOrWhiteSpace(module?.Id))
        {
            errors["Module"] =
                ["Module is required: pick a module by id"];
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            errors["Type"] =
                ["Type is required"];
        }

        return errors;
    }
}
