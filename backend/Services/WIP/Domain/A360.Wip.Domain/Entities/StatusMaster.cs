
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Wip.Domain.Entities;

public class StatusMaster : BaseEntity
{
    [BsonElement("status_id")]
    public string StatusId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string AssetId { get; set; } = null!;

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("status_name")]
    public string StatusName { get; set; } = null!;

    [BsonElement("status_code")]
    public string StatusCode { get; set; } = null!;

    [BsonElement("sequence_order")]
    public int? SequenceOrder { get; set; }

    [BsonElement("is_closed_status")]
    public bool IsClosedStatus { get; set; }

    [BsonElement("color_code")]
    public string? ColorCode { get; set; }

    [BsonElement("allowed_transitions")]
    public List<string> AllowedTransitions { get; set; } = [];

    [BsonElement("requires_approval")]
    public bool RequiresApproval { get; set; }

    [BsonElement("is_default")]
    public bool IsDefault { get; set; }
}
