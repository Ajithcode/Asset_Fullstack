using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

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
}
