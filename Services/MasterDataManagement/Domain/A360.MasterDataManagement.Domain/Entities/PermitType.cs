using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

public class PermitType : BaseEntity
{
    [BsonElement("permit_type_name")]
    public string PermitTypeName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // e.g. "Safety" or "Electrical".
    [BsonElement("category")]
    public string Category { get; set; } = null!;

    // How many days an issued permit stays valid.
    [BsonElement("valid_till_days")]
    public int? ValidTillDays { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }
}
