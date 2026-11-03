using MongoDB.Bson.Serialization.Attributes;

namespace A360.Asset.Domain.Entities;

/// <summary>Step 1 of asset registration: the asset's details and identification.</summary>
[BsonIgnoreExtraElements]
public sealed class AssetBasicInformation
{
    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("asset_tag")]
    public string? AssetTag { get; set; }

    [BsonElement("barcode")]
    public string? Barcode { get; set; }

    [BsonElement("rfid_tag_id")]
    public string? RfidTagId { get; set; }

    // Category, sub category and asset type are captured in step 2 (Classification).

    [BsonElement("model")]
    public string? Model { get; set; }

    [BsonElement("manufacturer")]
    public string? Manufacturer { get; set; }

    [BsonElement("serial_number")]
    public string? SerialNumber { get; set; }

    [BsonElement("status")]
    public string? Status { get; set; }

    [BsonElement("condition")]
    public string? Condition { get; set; }

    [BsonElement("criticality")]
    public string? Criticality { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // URLs returned by the Media service upload endpoint.
    [BsonElement("images")]
    public List<string> Images { get; set; } = [];
}
