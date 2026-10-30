using System.Text.RegularExpressions;
using A360.Administration.Api.Contracts;

namespace A360.Administration.Api.Validation;

public static partial class AccessPolicyValidationExtensions
{
    private const int DescriptionMaxLength = 500;

    private static readonly string[] AllowedPolicyTypes = ["Role", "Team", "User"];
    private static readonly string[] AllowedStatuses = ["Active", "Inactive"];

    public static Dictionary<string, string[]> Validate(this CreateAccessPolicyRequest request)
    {
        return ValidatePolicy(request.PolicyName, request.PolicyCode, request.Description, request.PolicyType, request.Status);
    }

    public static Dictionary<string, string[]> Validate(this UpdateAccessPolicyRequest request)
    {
        return ValidatePolicy(request.PolicyName, request.PolicyCode, request.Description, request.PolicyType, request.Status);
    }

    private static Dictionary<string, string[]> ValidatePolicy(
        string? policyName,
        string? policyCode,
        string? description,
        string? policyType,
        string? status)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, "PolicyName", policyName);
        Require(errors, "PolicyCode", policyCode);
        Require(errors, "PolicyType", policyType);
        Require(errors, "Status", status);

        if (!string.IsNullOrWhiteSpace(policyCode) && !PolicyCodePattern().IsMatch(policyCode.Trim()))
        {
            errors["PolicyCode"] = ["PolicyCode may only contain letters, digits, hyphens and underscores"];
        }

        if (description?.Trim().Length > DescriptionMaxLength)
        {
            errors["Description"] = [$"Description must be at most {DescriptionMaxLength} characters"];
        }

        AllowOnly(errors, "PolicyType", policyType, AllowedPolicyTypes);
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

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex PolicyCodePattern();
}
