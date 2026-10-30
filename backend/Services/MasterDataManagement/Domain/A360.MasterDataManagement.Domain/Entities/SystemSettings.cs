using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

// One record per client. Each settings tab is its own section, so tabs can be added and saved independently.
[BsonIgnoreExtraElements]
public class SystemSettings : BaseEntity
{
    [BsonElement("general")]
    public GeneralSettings General { get; set; } = new();

    [BsonElement("date_time")]
    public DateTimeSettings DateAndTime { get; set; } = new();

    [BsonElement("branding")]
    public BrandingSettings Branding { get; set; } = new();

    [BsonElement("file_upload")]
    public FileUploadSettings FileUpload { get; set; } = new();

    [BsonElement("session")]
    public SessionSettings Session { get; set; } = new();

    [BsonElement("defaults")]
    public DefaultValueSettings Defaults { get; set; } = new();

    [BsonElement("security")]
    public SecuritySettings Security { get; set; } = new();

    [BsonElement("email")]
    public EmailSettings Email { get; set; } = new();

    [BsonElement("localization")]
    public LocalizationSettings Localization { get; set; } = new();

    [BsonElement("numbering")]
    public NumberingSettings Numbering { get; set; } = new();

    [BsonElement("integration")]
    public IntegrationSettings Integration { get; set; } = new();

    [BsonElement("backup")]
    public BackupSettings Backup { get; set; } = new();

    [BsonElement("other")]
    public OtherSettings Other { get; set; } = new();
}

public class GeneralSettings
{
    [BsonElement("system_name")]
    public string SystemName { get; set; } = "Asset360";

    [BsonElement("system_url")]
    public string? SystemUrl { get; set; }

    // IANA time zone id, e.g. "Asia/Dubai".
    [BsonElement("default_time_zone")]
    public string DefaultTimeZone { get; set; } = "Asia/Dubai";

    // .NET date format, e.g. "dd-MMM-yyyy".
    [BsonElement("date_format")]
    public string DateFormat { get; set; } = "dd-MMM-yyyy";

    // "24Hour" or "12Hour".
    [BsonElement("time_format")]
    public string TimeFormat { get; set; } = "24Hour";

    // Language code, e.g. "en".
    [BsonElement("default_language")]
    public string DefaultLanguage { get; set; } = "en";

    // ISO 4217 currency code, e.g. "AED".
    [BsonElement("default_currency")]
    public string DefaultCurrency { get; set; } = "AED";

    [BsonElement("records_per_page")]
    public int RecordsPerPage { get; set; } = 10;

    [BsonElement("enable_audit_log")]
    public bool EnableAuditLog { get; set; } = true;

    [BsonElement("allow_offline_mode")]
    public bool AllowOfflineMode { get; set; } = true;

    [BsonElement("system_maintenance_mode")]
    public bool SystemMaintenanceMode { get; set; }
}

public class DateTimeSettings
{
    // e.g. "Monday".
    [BsonElement("week_starts_on")]
    public string WeekStartsOn { get; set; } = "Monday";

    // Month name, e.g. "January".
    [BsonElement("fiscal_year_start")]
    public string FiscalYearStart { get; set; } = "January";

    // "HH:mm".
    [BsonElement("workday_start")]
    public string WorkdayStart { get; set; } = "08:00";

    [BsonElement("workday_end")]
    public string WorkdayEnd { get; set; } = "17:00";
}

public class BrandingSettings
{
    // Image data URL or link.
    [BsonElement("logo")]
    public string? Logo { get; set; }

    [BsonElement("primary_color")]
    public string PrimaryColor { get; set; } = "#4f2bd8";

    [BsonElement("login_message")]
    public string? LoginMessage { get; set; } = "Welcome to Asset360";
}

public class FileUploadSettings
{
    [BsonElement("max_size_mb")]
    public int MaxSizeMb { get; set; } = 10;

    // e.g. "PDF", "JPG".
    [BsonElement("allowed_types")]
    public List<string> AllowedTypes { get; set; } = ["PDF", "JPG", "PNG", "DOCX", "XLSX", "CSV"];

    [BsonElement("virus_scan")]
    public bool VirusScan { get; set; } = true;
}

public class SessionSettings
{
    [BsonElement("timeout_minutes")]
    public int TimeoutMinutes { get; set; } = 30;

    [BsonElement("idle_warning_minutes")]
    public int IdleWarningMinutes { get; set; } = 5;

    [BsonElement("remember_me_days")]
    public int RememberMeDays { get; set; } = 14;

    [BsonElement("concurrent_sessions")]
    public bool ConcurrentSessions { get; set; } = true;
}

// Values new records start with. Update source and chart type are codes of those masters.
public class DefaultValueSettings
{
    [BsonElement("criticality")]
    public string? Criticality { get; set; } = "Medium";

    [BsonElement("condition")]
    public string? Condition { get; set; } = "New";

    [BsonElement("update_source")]
    public string? UpdateSource { get; set; }

    [BsonElement("chart_type")]
    public string? ChartType { get; set; }
}

public class SecuritySettings
{
    [BsonElement("min_password_length")]
    public int MinPasswordLength { get; set; } = 8;

    [BsonElement("require_uppercase")]
    public bool RequireUppercase { get; set; } = true;

    [BsonElement("require_number")]
    public bool RequireNumber { get; set; } = true;

    [BsonElement("require_symbol")]
    public bool RequireSymbol { get; set; }

    // 0 = passwords never expire.
    [BsonElement("password_expiry_days")]
    public int PasswordExpiryDays { get; set; } = 90;

    [BsonElement("lockout_attempts")]
    public int LockoutAttempts { get; set; } = 5;

    [BsonElement("mfa_required")]
    public bool MfaRequired { get; set; }

    // IPv4 addresses or ranges, one per line or comma separated.
    [BsonElement("ip_allowlist")]
    public string? IpAllowlist { get; set; }
}

public class EmailSettings
{
    [BsonElement("smtp_host")]
    public string? SmtpHost { get; set; }

    [BsonElement("smtp_port")]
    public int SmtpPort { get; set; } = 587;

    [BsonElement("sender_name")]
    public string? SenderName { get; set; } = "Asset360";

    [BsonElement("sender_email")]
    public string? SenderEmail { get; set; }

    [BsonElement("use_tls")]
    public bool UseTls { get; set; } = true;

    // Notification channels, e.g. "Email", "In-app".
    [BsonElement("channels")]
    public List<string> Channels { get; set; } = ["Email", "In-app"];

    // "Off", "Daily" or "Weekly".
    [BsonElement("digest")]
    public string Digest { get; set; } = "Daily";
}

public class LocalizationSettings
{
    // Enabled languages, e.g. "English", "Arabic".
    [BsonElement("languages")]
    public List<string> Languages { get; set; } = ["English", "Arabic"];

    [BsonElement("rtl")]
    public bool Rtl { get; set; } = true;

    [BsonElement("number_format")]
    public string NumberFormat { get; set; } = "1,234.56";

    [BsonElement("units")]
    public string Units { get; set; } = "Metric";
}

public class NumberingSettings
{
    [BsonElement("prefix")]
    public string Prefix { get; set; } = "AST";

    // e.g. "{PREFIX}-{YY}-{0000}".
    [BsonElement("pattern")]
    public string Pattern { get; set; } = "{PREFIX}-{YY}-{0000}";

    [BsonElement("start_number")]
    public int StartNumber { get; set; } = 1;

    [BsonElement("digits")]
    public int Digits { get; set; } = 4;

    [BsonElement("reset_yearly")]
    public bool ResetYearly { get; set; } = true;

    [BsonElement("include_site")]
    public bool IncludeSite { get; set; }
}

// Sources are update source codes; the failed sync status is an API sync status code.
public class IntegrationSettings
{
    [BsonElement("erp")]
    public string Erp { get; set; } = "None";

    [BsonElement("api_enabled")]
    public bool ApiEnabled { get; set; } = true;

    [BsonElement("rate_limit_per_minute")]
    public int RateLimitPerMinute { get; set; } = 120;

    [BsonElement("webhook_url")]
    public string? WebhookUrl { get; set; }

    [BsonElement("sync_interval_minutes")]
    public int SyncIntervalMinutes { get; set; } = 60;

    [BsonElement("sources")]
    public List<string> Sources { get; set; } = [];

    [BsonElement("failed_sync_status")]
    public string? FailedSyncStatus { get; set; }
}

public class BackupSettings
{
    // "Hourly", "Daily" or "Weekly".
    [BsonElement("frequency")]
    public string Frequency { get; set; } = "Daily";

    // "HH:mm".
    [BsonElement("time")]
    public string Time { get; set; } = "02:00";

    [BsonElement("retention_days")]
    public int RetentionDays { get; set; } = 30;

    [BsonElement("audit_retention_months")]
    public int AuditRetentionMonths { get; set; } = 24;

    [BsonElement("deleted_retention_days")]
    public int DeletedRetentionDays { get; set; } = 90;
}

public class OtherSettings
{
    [BsonElement("beta_features")]
    public bool BetaFeatures { get; set; }

    [BsonElement("helpdesk_email")]
    public string? HelpdeskEmail { get; set; }

    [BsonElement("support_phone")]
    public string? SupportPhone { get; set; }

    [BsonElement("terms_url")]
    public string? TermsUrl { get; set; }
}
