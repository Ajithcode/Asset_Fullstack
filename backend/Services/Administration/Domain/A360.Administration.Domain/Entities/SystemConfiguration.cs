using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

// One document per tenant/client holding the General Settings of the platform.
[BsonIgnoreExtraElements]
public sealed class SystemConfiguration : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("configuration_id")]
    public string ConfigurationId { get; set; } = string.Empty;

    [BsonElement("platform_identity")]
    public PlatformIdentitySettings PlatformIdentity { get; set; } = new();

    [BsonElement("regional_defaults")]
    public RegionalDefaultSettings RegionalDefaults { get; set; } = new();

    [BsonElement("user_experience")]
    public UserExperienceSettings UserExperience { get; set; } = new();

    [BsonElement("data_and_records")]
    public DataAndRecordSettings DataAndRecords { get; set; } = new();
}

[BsonIgnoreExtraElements]
public sealed class PlatformIdentitySettings
{
    // Name displayed across the platform.
    [BsonElement("platform_name")]
    public string PlatformName { get; set; } = string.Empty;

    // Organization's legal name.
    [BsonElement("organization_name")]
    public string OrganizationName { get; set; } = string.Empty;

    // URL of the logo image, typically returned by the Media service upload.
    [BsonElement("logo")]
    public string? Logo { get; set; }

    // Page users see after login.
    [BsonElement("default_landing_page")]
    public string? DefaultLandingPage { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class RegionalDefaultSettings
{
    [BsonElement("country")]
    public string? Country { get; set; }

    // IANA time zone id, e.g. Asia/Dubai.
    [BsonElement("time_zone")]
    public string? TimeZone { get; set; }

    // e.g. DD/MM/YYYY.
    [BsonElement("date_format")]
    public string? DateFormat { get; set; }

    // ISO 4217 code, e.g. AED.
    [BsonElement("currency")]
    public string? Currency { get; set; }

    [BsonElement("language")]
    public string? Language { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class UserExperienceSettings
{
    // Light, Dark or System.
    [BsonElement("theme")]
    public string Theme { get; set; } = "Light";

    [BsonElement("compact_mode")]
    public bool CompactMode { get; set; }

    [BsonElement("default_rows_per_page")]
    public int DefaultRowsPerPage { get; set; } = 25;

    [BsonElement("enable_tooltips")]
    public bool EnableTooltips { get; set; } = true;
}

[BsonIgnoreExtraElements]
public sealed class DataAndRecordSettings
{
    // Default status for new records.
    [BsonElement("default_status")]
    public string DefaultStatus { get; set; } = "Active";

    // 0 disables auto-save.
    [BsonElement("auto_save_interval_seconds")]
    public int AutoSaveIntervalSeconds { get; set; } = 30;

    [BsonElement("require_delete_confirmation")]
    public bool RequireDeleteConfirmation { get; set; } = true;
}
