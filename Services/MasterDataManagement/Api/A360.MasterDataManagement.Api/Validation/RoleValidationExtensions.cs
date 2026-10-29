using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class RoleValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateRoleRequest request)
    {
        return ValidateFields(request.RoleName, request.Code, request.Type, request.Permissions, request.ModuleAccess);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateRoleRequest request)
    {
        return ValidateFields(request.RoleName, request.Code, request.Type, request.Permissions, request.ModuleAccess);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateRoleStatusRequest request)
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
        string? roleName,
        string? code,
        string? type,
        List<MasterDataReferenceDto>? permissions,
        List<MasterDataReferenceDto>? moduleAccess)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(roleName))
        {
            errors["RoleName"] =
                ["RoleName is required"];
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

        if (permissions is not null)
        {
            if (permissions.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id)))
            {
                errors["Permissions"] =
                    ["Every entry in Permissions needs a permission id"];
            }
            else if (permissions.GroupBy(x => x.Id!.Trim()).Any(x => x.Count() > 1))
            {
                errors["Permissions"] =
                    ["Permissions contains duplicate entries"];
            }
        }

        if (moduleAccess is not null)
        {
            if (moduleAccess.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id)))
            {
                errors["ModuleAccess"] =
                    ["Every entry in ModuleAccess needs a module id"];
            }
            else if (moduleAccess.GroupBy(x => x.Id!.Trim()).Any(x => x.Count() > 1))
            {
                errors["ModuleAccess"] =
                    ["ModuleAccess contains duplicate entries"];
            }
        }

        return errors;
    }
}
