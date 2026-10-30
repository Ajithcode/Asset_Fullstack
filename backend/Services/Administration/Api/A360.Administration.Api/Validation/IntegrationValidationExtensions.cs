using System.Text.RegularExpressions;
using A360.Administration.Api.Contracts;

namespace A360.Administration.Api.Validation;

public static partial class IntegrationValidationExtensions
{
    private const int MaxTimeoutSeconds = 3600;

    private static readonly string[] AllowedEnvironments = ["Development", "Staging", "Production"];
    private static readonly string[] AllowedStatuses = ["Active", "Inactive"];

    public static Dictionary<string, string[]> Validate(this CreateIntegrationRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, nameof(request.IntegrationName), request.IntegrationName);
        Require(errors, nameof(request.IntegrationCode), request.IntegrationCode);
        Require(errors, nameof(request.Provider), request.Provider);
        Require(errors, nameof(request.Environment), request.Environment);
        Require(errors, nameof(request.BaseUrl), request.BaseUrl);
        Require(errors, nameof(request.AuthenticationMethod), request.AuthenticationMethod);
        Require(errors, nameof(request.AuthClientId), request.AuthClientId);
        Require(errors, nameof(request.SecretKey), request.SecretKey);
        Require(errors, nameof(request.SyncFrequency), request.SyncFrequency);
        Require(errors, nameof(request.RetryPolicy), request.RetryPolicy);
        Require(errors, nameof(request.Status), request.Status);

        if (!string.IsNullOrWhiteSpace(request.IntegrationCode) && !CodePattern().IsMatch(request.IntegrationCode.Trim()))
        {
            errors[nameof(request.IntegrationCode)] = ["IntegrationCode may only contain letters, digits, hyphens and underscores"];
        }

        if (!string.IsNullOrWhiteSpace(request.BaseUrl)
            && (!Uri.TryCreate(request.BaseUrl.Trim(), UriKind.Absolute, out var baseUrl)
                || (baseUrl.Scheme != Uri.UriSchemeHttps && baseUrl.Scheme != Uri.UriSchemeHttp)))
        {
            errors[nameof(request.BaseUrl)] = ["BaseUrl must be an absolute http(s) URL"];
        }

        if (request.TimeoutSeconds is null)
        {
            errors[nameof(request.TimeoutSeconds)] = ["TimeoutSeconds is required"];
        }
        else if (request.TimeoutSeconds is < 1 or > MaxTimeoutSeconds)
        {
            errors[nameof(request.TimeoutSeconds)] = [$"TimeoutSeconds must be between 1 and {MaxTimeoutSeconds}"];
        }

        AllowOnly(errors, nameof(request.Environment), request.Environment, AllowedEnvironments);
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
    private static partial Regex CodePattern();
}
