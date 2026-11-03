
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Wip.Domain.Entities;

public class PermitMaster : BaseEntity
{
    [BsonElement("permit_id")]
    public string PermitId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string? AssetId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("job_id")]
    public string? JobId { get; set; }

    [BsonElement("permit_type")]
    public string? PermitType { get; set; }

    [BsonElement("issued_by")]
    public string? IssuedBy { get; set; }

    [BsonElement("approved_by")]
    public string? ApprovedBy { get; set; }

    [BsonElement("valid_from")]
    public DateTime? ValidFrom { get; set; }

    [BsonElement("valid_to")]
    public DateTime? ValidTo { get; set; }

    [BsonElement("document_attachment")]
    public string? DocumentAttachment { get; set; }
}
