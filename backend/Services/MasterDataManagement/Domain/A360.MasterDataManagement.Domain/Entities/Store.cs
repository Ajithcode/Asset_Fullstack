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

    // e.g. "Spare Parts", "Consumables" or "Tools".
    [BsonElement("store_type")]
    public string? StoreType { get; set; }

    // Points at an employee in this service.
    [BsonElement("keeper")]
    public MasterDataReference? Keeper { get; set; }

    [BsonElement("temperature_controlled")]
    public bool TemperatureControlled { get; set; }

    [BsonElement("restricted_access")]
    public bool RestrictedAccess { get; set; }
}
