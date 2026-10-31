using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

// A vendor holding an annual maintenance contract (AMC).
public class AmcVendor : BaseEntity
{
    [BsonElement("amc_vendor_name")]
    public string AmcVendorName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Contract period; both days are included.
    [BsonElement("start_date")]
    public DateOnly StartDate { get; set; }

    [BsonElement("end_date")]
    public DateOnly EndDate { get; set; }

    // Name of the main contact person.
    [BsonElement("primary_contact")]
    public string? PrimaryContact { get; set; }

    // Points at an asset type in this service: the kind of asset the contract covers.
    [BsonElement("asset_type")]
    public MasterDataReference? AssetType { get; set; }

    [BsonElement("email")]
    public string? Email { get; set; }

    [BsonElement("contract_type")]
    public string? ContractType { get; set; }

    // Logo as an image URL or data URL.
    [BsonElement("logo")]
    public string? Logo { get; set; }

    // Hex colour of the badge shown when there is no logo.
    [BsonElement("brand_color")]
    public string? BrandColor { get; set; }

    [BsonElement("website")]
    public string? Website { get; set; }

    [BsonElement("phone")]
    public string? Phone { get; set; }

    [BsonElement("address")]
    public StructureAddress? Address { get; set; }

    [BsonElement("alternate_contact")]
    public string? AlternateContact { get; set; }

    [BsonElement("alternate_email")]
    public string? AlternateEmail { get; set; }

    [BsonElement("alternate_phone")]
    public string? AlternatePhone { get; set; }

    [BsonElement("documents")]
    public List<VendorDocument> Documents { get; set; } = [];

    [BsonElement("contract_no")]
    public string? ContractNo { get; set; }

    [BsonElement("contract_value")]
    public decimal? ContractValue { get; set; }

    [BsonElement("currency")]
    public string? Currency { get; set; }

    [BsonElement("billing_cycle")]
    public string? BillingCycle { get; set; }

    // Days before the end date to flag the contract for renewal.
    [BsonElement("renewal_notice_days")]
    public int RenewalNoticeDays { get; set; }

    [BsonElement("auto_renew")]
    public bool AutoRenew { get; set; }

    // Codes of the asset models the contract covers.
    [BsonElement("covered_models")]
    public List<string> CoveredModels { get; set; } = [];

    [BsonElement("visits_per_year")]
    public int VisitsPerYear { get; set; }

    [BsonElement("response_hours")]
    public int ResponseHours { get; set; }

    [BsonElement("resolution_hours")]
    public int ResolutionHours { get; set; }

    [BsonElement("uptime_percent")]
    public decimal UptimePercent { get; set; }

    [BsonElement("penalty_per_hour")]
    public decimal? PenaltyPerHour { get; set; }

    [BsonElement("notify_before_expiry")]
    public bool NotifyBeforeExpiry { get; set; }

    [BsonElement("service_report_required")]
    public bool ServiceReportRequired { get; set; }
}
