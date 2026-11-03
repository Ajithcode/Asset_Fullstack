using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

// A single action a role can be allowed to perform, e.g. "Create Asset".
public class Permission : BaseEntity
{
    [BsonElement("permission_name")]
    public string PermissionName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Points at a module in this service.
    [BsonElement("module")]
    public MasterDataReference? Module { get; set; }

    // e.g. "Read", "Create", "Update", "Delete", "Approve", "Manage" or "Execute".
    [BsonElement("type")]
    public string Type { get; set; } = null!;

    [BsonElement("description")]
    public string? Description { get; set; }
}
