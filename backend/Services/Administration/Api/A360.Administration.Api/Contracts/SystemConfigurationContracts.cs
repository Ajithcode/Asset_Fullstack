using A360.Administration.Domain.Entities;

namespace A360.Administration.Api.Contracts;

public sealed record SaveSystemConfigurationRequest(
    PlatformIdentityRequest? PlatformIdentity,
    RegionalDefaultsRequest? RegionalDefaults,
    UserExperienceRequest? UserExperience,
    DataAndRecordsRequest? DataAndRecords,
    string? UpdatedBy,
    string? TenantId,
    string? ClientId);

public sealed record PlatformIdentityRequest(
    string? PlatformName,
    string? OrganizationName,
    string? Logo,
    string? DefaultLandingPage);

public sealed record RegionalDefaultsRequest(
    string? Country,
    string? TimeZone,
    string? DateFormat,
    string? Currency,
    string? Language);

public sealed record UserExperienceRequest(
    string? Theme,
    bool? CompactMode,
    int? DefaultRowsPerPage,
    bool? EnableTooltips);

public sealed record DataAndRecordsRequest(
    string? DefaultStatus,
    int? AutoSaveIntervalSeconds,
    bool? RequireDeleteConfirmation);

public sealed record SystemConfigurationResponse(
    string ConfigurationId,
    PlatformIdentitySettings PlatformIdentity,
    RegionalDefaultSettings RegionalDefaults,
    UserExperienceSettings UserExperience,
    DataAndRecordSettings DataAndRecords,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? TenantId,
    string? ClientId)
{
    public static SystemConfigurationResponse FromEntity(SystemConfiguration configuration)
    {
        return new SystemConfigurationResponse(
            configuration.ConfigurationId,
            configuration.PlatformIdentity,
            configuration.RegionalDefaults,
            configuration.UserExperience,
            configuration.DataAndRecords,
            configuration.CreatedBy,
            configuration.CreatedAt,
            configuration.UpdatedBy,
            configuration.UpdatedAt,
            configuration.TenantId,
            configuration.ClientId);
    }
}
