using A360.MasterDataManagement.Api.Contracts;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Api.Validation;

public static class RoleValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateRoleRequest request)
    {
        var errors = ValidateFields(request.RoleName, request.Code, request.Permissions, request.ModuleAccess);
        ValidateAccess(errors, request.ModulePermissions, request.DataScope, request.Locations, request.Departments, request.ApprovalLimit);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateRoleRequest request)
    {
        var errors = ValidateFields(request.RoleName, request.Code, request.Permissions, request.ModuleAccess);
        ValidateAccess(errors, request.ModulePermissions, request.DataScope, request.Locations, request.Departments, request.ApprovalLimit);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateRolePermissionMatrixRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.ModulePermissions is null)
        {
            errors["ModulePermissions"] =
                ["ModulePermissions is required"];
            return errors;
        }

        ValidateModulePermissions(errors, request.ModulePermissions);
        return errors;
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

    // Type is optional and defaults to "Custom Role".
    private static void ValidateAccess(
        Dictionary<string, string[]> errors,
        List<RoleModulePermissionDto>? modulePermissions,
        string? dataScope,
        List<MasterDataReferenceDto>? locations,
        List<MasterDataReferenceDto>? departments,
        decimal? approvalLimit)
    {
        if (modulePermissions is not null)
        {
            ValidateModulePermissions(errors, modulePermissions);
        }

        var scope = RoleContractDefaults.DataScope(dataScope);

        if (!RoleDataScopes.All.Contains(scope))
        {
            errors["DataScope"] =
                [$"DataScope must be one of: {string.Join(", ", RoleDataScopes.All)}"];
        }

        if (scope == RoleDataScopes.AssignedLocations && (locations is null || locations.Count == 0))
        {
            errors["Locations"] =
                ["Select at least one location when DataScope is 'Assigned Locations'"];
        }

        ValidateIdList(errors, "Locations", "location", locations);
        ValidateIdList(errors, "Departments", "department", departments);

        if (approvalLimit is < 0)
        {
            errors["ApprovalLimit"] =
                ["ApprovalLimit cannot be negative"];
        }
    }

    private static void ValidateModulePermissions(
        Dictionary<string, string[]> errors,
        List<RoleModulePermissionDto> modulePermissions)
    {
        if (modulePermissions.Any(x => x is null || string.IsNullOrWhiteSpace(x.ModuleId)))
        {
            errors["ModulePermissions"] =
                ["Every entry in ModulePermissions needs a module id"];
        }
        else if (modulePermissions.GroupBy(x => x.ModuleId!.Trim()).Any(x => x.Count() > 1))
        {
            errors["ModulePermissions"] =
                ["ModulePermissions contains the same module more than once"];
        }
    }

    private static void ValidateIdList(
        Dictionary<string, string[]> errors,
        string field,
        string label,
        List<MasterDataReferenceDto>? references)
    {
        if (references is null)
        {
            return;
        }

        if (references.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id)))
        {
            errors[field] =
                [$"Every entry in {field} needs a {label} id"];
        }
        else if (references.GroupBy(x => x.Id!.Trim()).Any(x => x.Count() > 1))
        {
            errors[field] =
                [$"{field} contains duplicate entries"];
        }
    }
}
