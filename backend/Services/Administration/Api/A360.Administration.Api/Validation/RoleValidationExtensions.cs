using System.Text.RegularExpressions;
using A360.Administration.Api.Contracts;

namespace A360.Administration.Api.Validation;

public static partial class RoleValidationExtensions
{
    private static readonly string[] AllowedRoleTypes = ["System", "Custom"];
    private static readonly string[] AllowedStatuses = ["Active", "Inactive"];

    public static Dictionary<string, string[]> Validate(this CreateRoleRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, nameof(request.RoleName), request.RoleName);
        Require(errors, nameof(request.RoleCode), request.RoleCode);
        Require(errors, nameof(request.RoleType), request.RoleType);
        Require(errors, nameof(request.Status), request.Status);

        if (!string.IsNullOrWhiteSpace(request.RoleCode) && !RoleCodePattern().IsMatch(request.RoleCode.Trim()))
        {
            errors[nameof(request.RoleCode)] = ["RoleCode may only contain letters, digits and underscores"];
        }

        AllowOnly(errors, nameof(request.RoleType), request.RoleType, AllowedRoleTypes);
        AllowOnly(errors, nameof(request.Status), request.Status, AllowedStatuses);

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
