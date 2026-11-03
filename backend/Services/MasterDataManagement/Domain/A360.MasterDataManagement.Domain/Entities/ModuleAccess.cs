using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

// An application module (e.g. "Assets" or "Inventory") whose visibility can be granted to roles.
public class ModuleAccess : BaseEntity
{
    [BsonElement("module_name")]
    public string ModuleName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // e.g. "Asset Management" or "Operations".
    [BsonElement("category")]
    public string Category { get; set; } = null!;

    [BsonElement("description")]
    public string? Description { get; set; }

    // An icon name or an image URL, stored as given.
    [BsonElement("icon")]
    public string? Icon { get; set; }
}
