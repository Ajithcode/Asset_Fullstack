using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

// A status that an API synchronisation with an external system can end in.
public class ApiSyncStatus : BaseEntity
{
    [BsonElement("status_name")]
    public string StatusName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    [BsonElement("description")]
    public string? Description { get; set; }

    // Hex colour used to show the status, e.g. "#2E7D32".
    [BsonElement("color")]
    public string? Color { get; set; }

    // Whether a sync in this status counts as successful.
    [BsonElement("is_success")]
    public bool IsSuccess { get; set; }
}
