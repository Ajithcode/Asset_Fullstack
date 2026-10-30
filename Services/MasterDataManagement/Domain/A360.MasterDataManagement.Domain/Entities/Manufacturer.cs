using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

// A manufacturer's models are not stored here: each asset model points at its manufacturer.
public class Manufacturer : BaseEntity
{
    [BsonElement("manufacturer_name")]
    public string ManufacturerName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    [BsonElement("country")]
    public string? Country { get; set; }

    [BsonElement("website")]
    public string? Website { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("contact_person")]
    public string? ContactPerson { get; set; }

    [BsonElement("email")]
    public string? Email { get; set; }

    [BsonElement("phone_number")]
    public string? PhoneNumber { get; set; }

    // Uploaded logo as a data URL; a coloured badge with the initials is shown when empty.
    [BsonElement("logo")]
    public string? Logo { get; set; }

    [BsonElement("brand_color")]
    public string? BrandColor { get; set; }

    [BsonElement("designation")]
    public string? Designation { get; set; }

    [BsonElement("secondary_contact")]
    public string? SecondaryContact { get; set; }

    [BsonElement("secondary_email")]
    public string? SecondaryEmail { get; set; }

    [BsonElement("secondary_phone")]
    public string? SecondaryPhone { get; set; }

    // Headquarters address.
    [BsonElement("address")]
    public ManufacturerAddress Address { get; set; } = new();

    [BsonElement("support_portal")]
    public string? SupportPortal { get; set; }

    [BsonElement("support_email")]
    public string? SupportEmail { get; set; }

    [BsonElement("support_phone")]
    public string? SupportPhone { get; set; }

    [BsonElement("standard_warranty_months")]
    public int? StandardWarrantyMonths { get; set; }

    [BsonElement("sla_response_hours")]
    public int? SlaResponseHours { get; set; }

    [BsonElement("default_currency")]
    public string? DefaultCurrency { get; set; }

    [BsonElement("payment_terms")]
    public string? PaymentTerms { get; set; }

    [BsonElement("preferred")]
    public bool Preferred { get; set; }

    [BsonElement("warranty_registration")]
    public bool WarrantyRegistration { get; set; }

    [BsonElement("auto_create_warranty")]
    public bool AutoCreateWarranty { get; set; }
}

public class ManufacturerAddress
{
    [BsonElement("line1")]
    public string? Line1 { get; set; }

    [BsonElement("line2")]
    public string? Line2 { get; set; }

    [BsonElement("city")]
    public string? City { get; set; }

    [BsonElement("state")]
    public string? State { get; set; }

    [BsonElement("postal_code")]
    public string? PostalCode { get; set; }

    [BsonElement("country")]
    public string? Country { get; set; }
}
