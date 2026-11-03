using System.Text.RegularExpressions;
using A360.Administration.Api.Contracts;

namespace A360.Administration.Api.Validation;

public static partial class RoleValidationExtensions
{
    private static readonly string[] AllowedRoleTypes = ["System", "Custom"];
    private static readonly string[] AllowedStatuses = ["Active", "Inactive"];

    public static Dictionary<string, string[]> Validate(this CreateRoleRequest request)
    {
        return ValidateRole(request.RoleName, request.RoleCode, request.RoleType, request.Status);
    }

    public static Dictionary<string, string[]> Validate(this UpdateRoleRequest request)
    {
        return ValidateRole(request.RoleName, request.RoleCode, request.RoleType, request.Status);
    }

    private static Dictionary<string, string[]> ValidateRole(string? roleName, string? roleCode, string? roleType, string? status)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, "RoleName", roleName);
        Require(errors, "RoleCode", roleCode);
        Require(errors, "RoleType", roleType);
        Require(errors, "Status", status);

        if (!string.IsNullOrWhiteSpace(roleCode) && !RoleCodePattern().IsMatch(roleCode.Trim()))
        {
            errors["RoleCode"] = ["RoleCode may only contain letters, digits and underscores"];
        }

        AllowOnly(errors, "RoleType", roleType, AllowedRoleTypes);
        AllowOnly(errors, "Status", status, AllowedStatuses);

        return errors;
    }

    private static void Require(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = [$"{field} is required"];
        }
    }

    private static void AllowOnly(Dictionary<string, string[]> errors, string field, string? value, string[] allowed)
    {
        if (!string.IsNullOrWhiteSpace(value) && !allowed.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            errors[field] = [$"{field} must be one of: {string.Join(", ", allowed)}"];
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_]+$")]
    private static partial Regex RoleCodePattern();
}
