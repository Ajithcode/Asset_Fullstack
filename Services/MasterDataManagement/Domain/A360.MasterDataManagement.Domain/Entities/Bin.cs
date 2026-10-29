using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class Bin : BaseEntity
{
    [BsonElement("bin_name")]
    public string BinName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Points at a store in this service.
    [BsonElement("store")]
    public MasterDataReference? Store { get; set; }

    // Copied from the store, so bins can be listed by warehouse.
    [BsonElement("warehouse")]
    public MasterDataReference? Warehouse { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }
}
