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
}
