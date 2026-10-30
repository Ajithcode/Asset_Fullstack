using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class BusinessUnit : BaseEntity
{
    [BsonElement("business_unit_name")]
    public string BusinessUnitName { get; set; } = null!;

    // URL returned by the Media service (/api/media/upload).
    [BsonElement("logo_url")]
    public string? LogoUrl { get; set; }

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    [BsonElement("parent_organization")]
    public MasterDataReference? ParentOrganization { get; set; }

    [BsonElement("departments")]
    public List<MasterDataReference> Departments { get; set; } = [];

    [BsonElement("sites")]
    public List<MasterDataReference> Sites { get; set; } = [];

    // Optional parent business unit (in this collection) for sub-units.
    [BsonElement("parent_business_unit")]
    public MasterDataReference? ParentBusinessUnit { get; set; }

    // Built-in icon the UI shows when there is no logo.
    [BsonElement("icon")]
    public string? Icon { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("head")]
    public string? Head { get; set; }

    [BsonElement("cost_center")]
    public string? CostCenter { get; set; }

    [BsonElement("currency")]
    public string? Currency { get; set; }

    [BsonElement("address")]
    public StructureAddress? Address { get; set; }

    [BsonElement("contact_email")]
    public string? ContactEmail { get; set; }

    // Country whose dial code prefixes Phone.
    [BsonElement("phone_country")]
    public string? PhoneCountry { get; set; }

    [BsonElement("phone")]
    public string? Phone { get; set; }

    // 'yyyy-MM-dd'; EffectiveTo is optional.
    [BsonElement("effective_from")]
    public string? EffectiveFrom { get; set; }

    [BsonElement("effective_to")]
    public string? EffectiveTo { get; set; }

    [BsonElement("allow_sites")]
    public bool AllowSites { get; set; }

    [BsonElement("budget_tracking")]
    public bool BudgetTracking { get; set; }

    [BsonElement("asset_numbering")]
    public bool AssetNumbering { get; set; }

    [BsonElement("approvals")]
    public bool Approvals { get; set; }
}
