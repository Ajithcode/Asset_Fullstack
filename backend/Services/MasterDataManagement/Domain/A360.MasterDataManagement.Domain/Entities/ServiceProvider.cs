using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class ServiceProvider : BaseEntity
{
    [BsonElement("service_provider_name")]
    public string ServiceProviderName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    [BsonElement("website")]
    public string? Website { get; set; }

    // Name of the main contact person.
    [BsonElement("primary_contact")]
    public string? PrimaryContact { get; set; }

    [BsonElement("service_type")]
    public string? ServiceType { get; set; }

    [BsonElement("email")]
    public string? Email { get; set; }

    [BsonElement("country")]
    public string? Country { get; set; }

    [BsonElement("phone")]
    public string? Phone { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // Logo as an image URL or data URL.
    [BsonElement("logo")]
    public string? Logo { get; set; }

    // Hex colour of the badge shown when there is no logo.
    [BsonElement("brand_color")]
    public string? BrandColor { get; set; }

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

    // 0 to 5.
    [BsonElement("rating")]
    public decimal Rating { get; set; }

    // Service types offered besides the main one.
    [BsonElement("services")]
    public List<string> Services { get; set; } = [];

    // Skill names from the skill master.
    [BsonElement("skills")]
    public List<string> Skills { get; set; } = [];

    // Areas (emirates, countries) the provider covers.
    [BsonElement("coverage")]
    public List<string> Coverage { get; set; } = [];

    // Number of technicians the provider can send.
    [BsonElement("technicians")]
    public int Technicians { get; set; }

    [BsonElement("response_hours")]
    public int ResponseHours { get; set; }

    [BsonElement("licence_no")]
    public string? LicenceNo { get; set; }

    [BsonElement("licence_expiry")]
    public DateOnly? LicenceExpiry { get; set; }

    [BsonElement("insurance_policy")]
    public string? InsurancePolicy { get; set; }

    [BsonElement("insurance_expiry")]
    public DateOnly? InsuranceExpiry { get; set; }

    [BsonElement("iso_certified")]
    public bool IsoCertified { get; set; }

    [BsonElement("hse_approved")]
    public bool HseApproved { get; set; }

    [BsonElement("preferred")]
    public bool Preferred { get; set; }

    [BsonElement("approved")]
    public bool Approved { get; set; }

    [BsonElement("portal_access")]
    public bool PortalAccess { get; set; }

    [BsonElement("emergency_callout")]
    public bool EmergencyCallout { get; set; }
}
