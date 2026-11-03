using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

// A role in this service. It is separate from the UserAccount roles that control login.
public class Role : BaseEntity
{
    [BsonElement("role_name")]
    public string RoleName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // e.g. "System", "Business", "Operational" or "Read Only".
    [BsonElement("type")]
    public string Type { get; set; } = null!;

    [BsonElement("description")]
    public string? Description { get; set; }

    // Permissions in this service granted to the role.
    [BsonElement("permissions")]
    public List<MasterDataReference> Permissions { get; set; } = [];

    // Modules in this service the role can see.
    [BsonElement("module_access")]
    public List<MasterDataReference> ModuleAccess { get; set; } = [];
}
