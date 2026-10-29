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
        var errors = new Dictionary<string, string[]>();

        Require(errors, nameof(request.PolicyName), request.PolicyName);
        Require(errors, nameof(request.PolicyCode), request.PolicyCode);
        Require(errors, nameof(request.PolicyType), request.PolicyType);
        Require(errors, nameof(request.Status), request.Status);

        if (!string.IsNullOrWhiteSpace(request.PolicyCode) && !PolicyCodePattern().IsMatch(request.PolicyCode.Trim()))
        {
            errors[nameof(request.PolicyCode)] = ["PolicyCode may only contain letters, digits, hyphens and underscores"];
        }

        if (request.Description?.Trim().Length > DescriptionMaxLength)
        {
            errors[nameof(request.Description)] = [$"Description must be at most {DescriptionMaxLength} characters"];
        }

        AllowOnly(errors, nameof(request.PolicyType), request.PolicyType, AllowedPolicyTypes);
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

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex PolicyCodePattern();
}
