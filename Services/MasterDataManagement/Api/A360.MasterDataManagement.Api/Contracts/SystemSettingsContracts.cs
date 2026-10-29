using SystemSettingsEntity = A360.MasterDataManagement.Domain.Entities.SystemSettings;
using A360.MasterDataManagement.Domain.Entities;

namespace A360.MasterDataManagement.Api.Contracts;

public static class TimeFormats
{
    public static readonly string[] All = ["24Hour", "12Hour"];

    /// <summary>Returns the matching value in its standard spelling, or null when it is not one of <see cref="All"/>.</summary>
    public static string? Normalize(string? value)
    {
        return All.FirstOrDefault(x => x.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}

// Each section is optional: a section left out keeps its saved values, so a tab can be saved on its own.
public sealed record UpdateSystemSettingsRequest(
    string? ClientId,
    string? TenantId,
    string? UpdatedBy,
    GeneralSettingsDto? General)
{
    public void ApplyTo(SystemSettingsEntity settings)
    {
        if (General is not null)
        {
            settings.General = General.ToSettings();
        }

        settings.TenantId ??= TenantId;
        settings.UpdatedBy = UpdatedBy;
        settings.UpdatedAt = DateTime.UtcNow;
    }
}

// Every field is required when the section is sent, so saving the tab replaces the whole section.
public sealed record GeneralSettingsDto(
    string? SystemName,
    string? SystemUrl,
    string? DefaultTimeZone,
    string? DateFormat,
    string? TimeFormat,
    string? DefaultLanguage,
    string? DefaultCurrency,
    int? RecordsPerPage,
    bool? EnableAuditLog,
    bool? AllowOfflineMode,
    bool? SystemMaintenanceMode)
{
    public GeneralSettings ToSettings()
    {
        return new GeneralSettings
        {
            SystemName = SystemName!.Trim(),
            SystemUrl = string.IsNullOrWhiteSpace(SystemUrl) ? null : SystemUrl.Trim(),
            DefaultTimeZone = DefaultTimeZone!.Trim(),
            DateFormat = DateFormat!.Trim(),
            TimeFormat = TimeFormats.Normalize(TimeFormat)!,
            DefaultLanguage = DefaultLanguage!.Trim(),
            DefaultCurrency = DefaultCurrency!.Trim().ToUpperInvariant(),
            RecordsPerPage = RecordsPerPage!.Value,
            EnableAuditLog = EnableAuditLog ?? false,
            AllowOfflineMode = AllowOfflineMode ?? false,
            SystemMaintenanceMode = SystemMaintenanceMode ?? false
        };
    }

    public static GeneralSettingsDto FromSettings(GeneralSettings settings)
    {
        return new GeneralSettingsDto(
            settings.SystemName,
            settings.SystemUrl,
            settings.DefaultTimeZone,
            settings.DateFormat,
            settings.TimeFormat,
            settings.DefaultLanguage,
            settings.DefaultCurrency,
            settings.RecordsPerPage,
            settings.EnableAuditLog,
            settings.AllowOfflineMode,
            settings.SystemMaintenanceMode);
    }
}

// Id is null until the client's settings are saved for the first time; until then the defaults are returned.
public sealed record SystemSettingsResponse(
    string? Id,
    string? ClientId,
    string? TenantId,
    GeneralSettingsDto General,
    string? UpdatedBy,
    DateTime? UpdatedAt)
{
    public static SystemSettingsResponse FromEntity(SystemSettingsEntity settings)
    {
        return new SystemSettingsResponse(
            string.IsNullOrEmpty(settings.Id) ? null : settings.Id,
            settings.ClientId,
            settings.TenantId,
            GeneralSettingsDto.FromSettings(settings.General ?? new GeneralSettings()),
            settings.UpdatedBy,
            settings.UpdatedAt);
    }
}
