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
        var errors = ValidateSettings(
            request.IntegrationName,
            request.IntegrationCode,
            request.Provider,
            request.Environment,
            request.BaseUrl,
            request.AuthenticationMethod,
            request.AuthClientId,
            request.SyncFrequency,
            request.TimeoutSeconds,
            request.RetryPolicy,
            request.Status);

        Require(errors, nameof(request.SecretKey), request.SecretKey);

        return errors;
    }

    // SecretKey is optional on update: blank keeps the stored secret.
    public static Dictionary<string, string[]> Validate(this UpdateIntegrationRequest request)
    {
        return ValidateSettings(
            request.IntegrationName,
            request.IntegrationCode,
            request.Provider,
            request.Environment,
            request.BaseUrl,
            request.AuthenticationMethod,
            request.AuthClientId,
            request.SyncFrequency,
            request.TimeoutSeconds,
            request.RetryPolicy,
            request.Status);
    }

    private static Dictionary<string, string[]> ValidateSettings(
        string? integrationName,
        string? integrationCode,
        string? provider,
        string? environment,
        string? baseUrl,
        string? authenticationMethod,
        string? authClientId,
        string? syncFrequency,
        int? timeoutSeconds,
        string? retryPolicy,
        string? status)
    {
        var errors = new Dictionary<string, string[]>();

        Require(errors, "IntegrationName", integrationName);
        Require(errors, "IntegrationCode", integrationCode);
        Require(errors, "Provider", provider);
        Require(errors, "Environment", environment);
        Require(errors, "BaseUrl", baseUrl);
        Require(errors, "AuthenticationMethod", authenticationMethod);
        Require(errors, "AuthClientId", authClientId);
        Require(errors, "SyncFrequency", syncFrequency);
        Require(errors, "RetryPolicy", retryPolicy);
        Require(errors, "Status", status);

        if (!string.IsNullOrWhiteSpace(integrationCode) && !CodePattern().IsMatch(integrationCode.Trim()))
        {
            errors["IntegrationCode"] = ["IntegrationCode may only contain letters, digits, hyphens and underscores"];
        }

        if (!string.IsNullOrWhiteSpace(baseUrl)
            && (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var parsedBaseUrl)
                || (parsedBaseUrl.Scheme != Uri.UriSchemeHttps && parsedBaseUrl.Scheme != Uri.UriSchemeHttp)))
        {
            errors["BaseUrl"] = ["BaseUrl must be an absolute http(s) URL"];
        }

        if (timeoutSeconds is null)
        {
            errors["TimeoutSeconds"] = ["TimeoutSeconds is required"];
        }
        else if (timeoutSeconds is < 1 or > MaxTimeoutSeconds)
        {
            errors["TimeoutSeconds"] = [$"TimeoutSeconds must be between 1 and {MaxTimeoutSeconds}"];
        }

        AllowOnly(errors, "Environment", environment, AllowedEnvironments);
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
    private static partial Regex CodePattern();
}
