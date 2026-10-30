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

    [BsonElement("address_line1")]
    public string? AddressLine1 { get; set; }

    [BsonElement("city")]
    public string? City { get; set; }

    [BsonElement("state")]
    public string? State { get; set; }

    [BsonElement("postal_code")]
    public string? PostalCode { get; set; }

    [BsonElement("country")]
    public string? Country { get; set; }

    [BsonElement("phone")]
    public string? Phone { get; set; }

    [BsonElement("email")]
    public string? Email { get; set; }

    // Floor area in square metres.
    [BsonElement("area_sqm")]
    public double? AreaSqm { get; set; }

    // e.g. "07:00 - 19:00".
    [BsonElement("operating_hours")]
    public string? OperatingHours { get; set; }

    [BsonElement("allow_transfers_out")]
    public bool AllowTransfersOut { get; set; } = true;
}
