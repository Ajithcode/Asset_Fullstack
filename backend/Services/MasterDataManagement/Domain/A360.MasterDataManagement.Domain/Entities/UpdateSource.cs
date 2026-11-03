using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

// A way asset and related data can be updated, e.g. manual entry or an IoT device.
public class UpdateSource : BaseEntity
{
    [BsonElement("source_name")]
    public string SourceName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // e.g. "Manual", "Device", "External", "File", "API" or "System".
    [BsonElement("type")]
    public string Type { get; set; } = null!;

    [BsonElement("description")]
    public string? Description { get; set; }
}
