
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Wip.Domain.Entities;

public class ChecklistItem : BaseEntity
{
    [BsonElement("item_id")]
    public string ItemId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string? AssetId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("checklist_id")]
    public string? ChecklistId { get; set; }

    [BsonElement("item_description")]
    public string ItemDescription { get; set; } = null!;

    [BsonElement("response_type")]
    public string? ResponseType { get; set; }

    [BsonElement("threshold_value")]
    public double? ThresholdValue { get; set; }

    [BsonElement("is_critical")]
    public bool IsCritical { get; set; }

    [BsonElement("sequence_order")]
    public int? SequenceOrder { get; set; }
}
