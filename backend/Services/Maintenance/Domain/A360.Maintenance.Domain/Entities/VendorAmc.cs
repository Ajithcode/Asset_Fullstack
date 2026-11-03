
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Maintenance.Domain.Entities;

public class VendorAmc : BaseEntity
{
    [BsonElement("vendor_name")]
    public string VendorName { get; set; } = null!;

    [BsonElement("contract_id")]
    public string ContractId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string? AssetId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("assets_covered")]
    public List<string>? AssetsCovered { get; set; }

    [BsonElement("start_date")]
    public DateTime? StartDate { get; set; }

    [BsonElement("end_date")]
    public DateTime? EndDate { get; set; }

    [BsonElement("sla_terms")]
    public string? SlaTerms { get; set; }
}
