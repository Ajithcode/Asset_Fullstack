using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

// Status (Active, Inactive) is stored in BaseEntity.Status.
[BsonIgnoreExtraElements]
public sealed class Role : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("role_id")]
    public string RoleId { get; set; } = string.Empty;

    [BsonElement("role_name")]
    public string RoleName { get; set; } = string.Empty;

    // Unique, upper-case code such as OPS_MANAGER.
    [BsonElement("role_code")]
    public string RoleCode { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; }

    // System or Custom.
    [BsonElement("role_type")]
    public string RoleType { get; set; } = string.Empty;
}
