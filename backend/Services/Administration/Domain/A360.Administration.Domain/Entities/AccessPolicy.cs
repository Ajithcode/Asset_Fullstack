using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Administration.Domain.Entities;

// Status (Active, Inactive) is stored in BaseEntity.Status.
[BsonIgnoreExtraElements]
public sealed class AccessPolicy : BaseEntity
{
    // Business identifier exposed to clients; kept separate from the Mongo _id.
    [BsonElement("policy_id")]
    public string PolicyId { get; set; } = string.Empty;

    [BsonElement("policy_name")]
    public string PolicyName { get; set; } = string.Empty;

    // Unique, upper-case code such as OPS-MGR-001.
    [BsonElement("policy_code")]
    public string PolicyCode { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; }

    // Role, Team or User.
    [BsonElement("policy_type")]
    public string PolicyType { get; set; } = string.Empty;

    // RoleIds of the roles this policy applies to.
    [BsonElement("assigned_role_ids")]
    public List<string> AssignedRoleIds { get; set; } = [];
}
