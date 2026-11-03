using MongoDB.Bson.Serialization.Attributes;

namespace A360.MasterDataManagement.Domain.ValueObjects;

/// <summary>
/// Regional defaults and feature switches of an organization.
/// </summary>
public class OrganizationSettings
{
    [BsonElement("currency")]
    public string? Currency { get; set; }

    [BsonElement("time_zone")]
    public string? TimeZone { get; set; }

    [BsonElement("language")]
    public string? Language { get; set; }

    [BsonElement("date_format")]
    public string? DateFormat { get; set; }

    // 'yyyy-MM-dd'; EffectiveTo is optional.
    [BsonElement("effective_from")]
    public string? EffectiveFrom { get; set; }

    [BsonElement("effective_to")]
    public string? EffectiveTo { get; set; }

    [BsonElement("allow_sub_organizations")]
    public bool AllowSubOrganizations { get; set; }

    [BsonElement("allow_multiple_locations")]
    public bool AllowMultipleLocations { get; set; }

    [BsonElement("use_for_asset_numbering")]
    public bool UseForAssetNumbering { get; set; }

    [BsonElement("enable_financial_tracking")]
    public bool EnableFinancialTracking { get; set; }
}
