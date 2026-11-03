using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Contracts;

internal static class SystemConfigurationMappings
{
    // Replaces every section with the values sent; omitted optional values fall back to the defaults.
    public static void ApplyTo(this SaveSystemConfigurationRequest request, SystemConfiguration configuration)
    {
        var platformIdentity = request.PlatformIdentity!;
        configuration.PlatformIdentity = new PlatformIdentitySettings
        {
            PlatformName = platformIdentity.PlatformName!.Trim(),
            OrganizationName = platformIdentity.OrganizationName!.Trim(),
            Logo = Optional(platformIdentity.Logo),
            DefaultLandingPage = Optional(platformIdentity.DefaultLandingPage)
        };

        var regional = request.RegionalDefaults;
        configuration.RegionalDefaults = new RegionalDefaultSettings
        {
            Country = Optional(regional?.Country),
            TimeZone = Optional(regional?.TimeZone),
            DateFormat = Optional(regional?.DateFormat)?.ToUpperInvariant(),
            Currency = Optional(regional?.Currency)?.ToUpperInvariant(),
            Language = Optional(regional?.Language)
        };

        var userExperience = request.UserExperience;
        var userExperienceDefaults = new UserExperienceSettings();
        configuration.UserExperience = new UserExperienceSettings
        {
            Theme = Optional(userExperience?.Theme) ?? userExperienceDefaults.Theme,
            CompactMode = userExperience?.CompactMode ?? userExperienceDefaults.CompactMode,
            DefaultRowsPerPage = userExperience?.DefaultRowsPerPage ?? userExperienceDefaults.DefaultRowsPerPage,
            EnableTooltips = userExperience?.EnableTooltips ?? userExperienceDefaults.EnableTooltips
        };

        var dataAndRecords = request.DataAndRecords;
        var dataAndRecordsDefaults = new DataAndRecordSettings();
        configuration.DataAndRecords = new DataAndRecordSettings
        {
            DefaultStatus = Optional(dataAndRecords?.DefaultStatus) ?? dataAndRecordsDefaults.DefaultStatus,
            AutoSaveIntervalSeconds = dataAndRecords?.AutoSaveIntervalSeconds ?? dataAndRecordsDefaults.AutoSaveIntervalSeconds,
            RequireDeleteConfirmation = dataAndRecords?.RequireDeleteConfirmation ?? dataAndRecordsDefaults.RequireDeleteConfirmation
        };
    }

    private static string? Optional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
