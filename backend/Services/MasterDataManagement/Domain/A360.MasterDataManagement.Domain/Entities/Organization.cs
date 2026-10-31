using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class Organization : BaseEntity
{
    [BsonElement("organization_name")]
    public string OrganizationName { get; set; } = null!;

    // URL returned by the Media service (/api/media/upload).
    [BsonElement("logo_url")]
    public string? LogoUrl { get; set; }

    [BsonElement("code")]
    public string? Code { get; set; }

    [BsonElement("type")]
    public string? Type { get; set; }

    [BsonElement("registration_number")]
    public string? RegistrationNumber { get; set; }

    [BsonElement("website")]
    public string? Website { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("address")]
    public StructureAddress? Address { get; set; }

    [BsonElement("contact")]
    public StructureContact? Contact { get; set; }

    [BsonElement("settings")]
    public OrganizationSettings? Settings { get; set; }

    // Points at another organization in this collection.
    [BsonElement("parent_organization")]
    public MasterDataReference? ParentOrganization { get; set; }

    [BsonElement("business_units")]
    public List<MasterDataReference> BusinessUnits { get; set; } = [];

    [BsonElement("sites")]
    public List<MasterDataReference> Sites { get; set; } = [];
}
