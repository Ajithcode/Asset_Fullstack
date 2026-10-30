using System.Text.RegularExpressions;
using A360.Administration.Api.Contracts;

namespace A360.Administration.Api.Validation;

public static partial class SystemConfigurationValidationExtensions
{
    private const int MaxAutoSaveIntervalSeconds = 3600;

    private static readonly string[] AllowedThemes = ["Light", "Dark", "System"];
    private static readonly string[] AllowedDateFormats = ["DD/MM/YYYY", "MM/DD/YYYY", "YYYY-MM-DD", "DD-MM-YYYY", "DD.MM.YYYY"];
    private static readonly int[] AllowedRowsPerPage = [10, 25, 50, 100];

    public static Dictionary<string, string[]> Validate(this SaveSystemConfigurationRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.PlatformIdentity is null)
        {
            errors[nameof(request.PlatformIdentity)] = ["PlatformIdentity is required"];
        }
        else
        {
            Require(errors, "PlatformIdentity.PlatformName", request.PlatformIdentity.PlatformName);
            Require(errors, "PlatformIdentity.OrganizationName", request.PlatformIdentity.OrganizationName);
        }

        var regional = request.RegionalDefaults;
        if (!string.IsNullOrWhiteSpace(regional?.TimeZone)
            && !TimeZoneInfo.TryFindSystemTimeZoneById(regional.TimeZone.Trim(), out _))
        {
            errors["RegionalDefaults.TimeZone"] = ["TimeZone must be a valid IANA time zone id, e.g. Asia/Dubai"];
        }

        AllowOnly(errors, "RegionalDefaults.DateFormat", regional?.DateFormat, AllowedDateFormats);

        if (!string.IsNullOrWhiteSpace(regional?.Currency) && !CurrencyPattern().IsMatch(regional.Currency.Trim()))
        {
            errors["RegionalDefaults.Currency"] = ["Currency must be a 3-letter ISO 4217 code, e.g. AED"];
        }

        var userExperience = request.UserExperience;
        AllowOnly(errors, "UserExperience.Theme", userExperience?.Theme, AllowedThemes);

        if (userExperience?.DefaultRowsPerPage is { } rowsPerPage && !AllowedRowsPerPage.Contains(rowsPerPage))
        {
            errors["UserExperience.DefaultRowsPerPage"] = [$"DefaultRowsPerPage must be one of: {string.Join(", ", AllowedRowsPerPage)}"];
        }

        if (request.DataAndRecords?.AutoSaveIntervalSeconds is < 0 or > MaxAutoSaveIntervalSeconds)
        {
            errors["DataAndRecords.AutoSaveIntervalSeconds"] =
                [$"AutoSaveIntervalSeconds must be between 0 (off) and {MaxAutoSaveIntervalSeconds}"];
        }

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

    [GeneratedRegex("^[A-Za-z]{3}$")]
    private static partial Regex CurrencyPattern();
}
