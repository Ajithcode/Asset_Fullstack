using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

public class MaintenanceType : BaseEntity
{
    [BsonElement("maintenance_type_name")]
    public string MaintenanceTypeName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    [BsonElement("description")]
    public string Description { get; set; } = null!;

    // Hex colour used to show the type, e.g. "#1565C0".
    [BsonElement("color")]
    public string? Color { get; set; }
}
