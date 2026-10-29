using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class Store : BaseEntity
{
    [BsonElement("store_name")]
    public string StoreName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Points at a warehouse in this service.
    [BsonElement("warehouse")]
    public MasterDataReference? Warehouse { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }
}
