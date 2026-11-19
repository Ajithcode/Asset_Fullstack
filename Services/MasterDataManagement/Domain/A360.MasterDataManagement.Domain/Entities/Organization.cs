using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class Organization : BaseEntity
{
    [BsonElement("organization_name")]
    public string OrganizationName { get; set; } = null!;

    [BsonElement("code")]
    public string? Code { get; set; }

    [BsonElement("registration_number")]
    public string? RegistrationNumber { get; set; }

    [BsonElement("website")]
    public string? Website { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("address")]
    public OrganizationAddress? Address { get; set; }

    [BsonElement("contact")]
    public OrganizationContact? Contact { get; set; }

    [BsonElement("settings")]
    public OrganizationSettings? Settings { get; set; }

    // URL returned by the Media service (/api/media/upload).
    [BsonElement("logo_url")]
    public string? LogoUrl { get; set; }

    [BsonElement("type")]
    public string? Type { get; set; }

    // Points at another organization in this collection.
    [BsonElement("parent_organization")]
    public MasterDataReference? ParentOrganization { get; set; }

    [BsonElement("business_units")]
    public List<MasterDataReference> BusinessUnits { get; set; } = [];

    [BsonElement("sites")]
    public List<MasterDataReference> Sites { get; set; } = [];
}

public class OrganizationAddress
{
    [BsonElement("line1")] public string? Line1 { get; set; }
    [BsonElement("line2")] public string? Line2 { get; set; }
    [BsonElement("city")] public string? City { get; set; }
    [BsonElement("state")] public string? State { get; set; }
    [BsonElement("country")] public string? Country { get; set; }
    [BsonElement("postal_code")] public string? PostalCode { get; set; }
}

public class OrganizationContact
{
    [BsonElement("name")] public string? Name { get; set; }
    [BsonElement("designation")] public string? Designation { get; set; }
    [BsonElement("email")] public string? Email { get; set; }
    [BsonElement("phone_country")] public string? PhoneCountry { get; set; }
    [BsonElement("phone")] public string? Phone { get; set; }
}

public class OrganizationSettings
{
    [BsonElement("currency")] public string? Currency { get; set; }
    [BsonElement("time_zone")] public string? TimeZone { get; set; }
    [BsonElement("language")] public string? Language { get; set; }
    [BsonElement("date_format")] public string? DateFormat { get; set; }
    // 'YYYY-MM-DD', as the form sends it
    [BsonElement("effective_from")] public string? EffectiveFrom { get; set; }
    [BsonElement("effective_to")] public string? EffectiveTo { get; set; }
    [BsonElement("allow_sub_organizations")] public bool AllowSubOrganizations { get; set; }
    [BsonElement("allow_multiple_locations")] public bool AllowMultipleLocations { get; set; }
    [BsonElement("use_for_asset_numbering")] public bool UseForAssetNumbering { get; set; }
    [BsonElement("enable_financial_tracking")] public bool EnableFinancialTracking { get; set; }
}
