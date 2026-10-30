using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class Supplier : BaseEntity
{
    [BsonElement("supplier_name")]
    public string SupplierName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    [BsonElement("website")]
    public string? Website { get; set; }

    // URL of an image uploaded through /api/media/upload.
    [BsonElement("supplier_logo")]
    public string? SupplierLogo { get; set; }

    // Name of the main contact person.
    [BsonElement("primary_contact")]
    public string? PrimaryContact { get; set; }

    [BsonElement("category")]
    public string? Category { get; set; }

    [BsonElement("email")]
    public string? Email { get; set; }

    [BsonElement("country")]
    public string? Country { get; set; }

    [BsonElement("phone")]
    public string? Phone { get; set; }

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

    [BsonElement("products")]
    public List<string> Products { get; set; } = [];

    // Asset category names the supplier provides.
    [BsonElement("asset_categories")]
    public List<string> AssetCategories { get; set; } = [];

    // Manufacturer (brand) names the supplier sells.
    [BsonElement("brands")]
    public List<string> Brands { get; set; } = [];

    [BsonElement("currency")]
    public string? Currency { get; set; }

    [BsonElement("payment_terms")]
    public string? PaymentTerms { get; set; }

    [BsonElement("credit_limit")]
    public decimal? CreditLimit { get; set; }

    // Tax registration number.
    [BsonElement("trn")]
    public string? Trn { get; set; }

    [BsonElement("bank_name")]
    public string? BankName { get; set; }

    [BsonElement("iban")]
    public string? Iban { get; set; }

    [BsonElement("preferred")]
    public bool Preferred { get; set; }

    [BsonElement("approved")]
    public bool Approved { get; set; }

    [BsonElement("portal_access")]
    public bool PortalAccess { get; set; }

    [BsonElement("email_purchase_orders")]
    public bool EmailPurchaseOrders { get; set; }
}
