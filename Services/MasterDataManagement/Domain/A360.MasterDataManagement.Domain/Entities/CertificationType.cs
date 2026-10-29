using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

public class CertificationType : BaseEntity
{
    [BsonElement("certification_type_name")]
    public string CertificationTypeName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // e.g. "Management System" or "Health & Safety".
    [BsonElement("category")]
    public string Category { get; set; } = null!;

    // What the certification can be held by: any of CertificationApplicability.All.
    [BsonElement("applicable_to")]
    public List<string> ApplicableTo { get; set; } = [];

    // How long a certificate stays valid.
    [BsonElement("validity_months")]
    public int? ValidityMonths { get; set; }

    // How many days before expiry to remind about renewal.
    [BsonElement("renewal_reminder_days")]
    public int? RenewalReminderDays { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }
}
