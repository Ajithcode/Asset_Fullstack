using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

// One record per client. Each settings tab is its own section, so tabs can be added and saved independently.
[BsonIgnoreExtraElements]
public class SystemSettings : BaseEntity
{
    [BsonElement("general")]
    public GeneralSettings General { get; set; } = new();
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
