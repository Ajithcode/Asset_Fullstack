using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class Warehouse : BaseEntity
{
    [BsonElement("warehouse_name")]
    public string WarehouseName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Where the warehouse is, e.g. "Dubai - HQ".
    [BsonElement("location")]
    public string? Location { get; set; }

    // Points at an employee in this service.
    [BsonElement("manager")]
    public MasterDataReference? Manager { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }
}
