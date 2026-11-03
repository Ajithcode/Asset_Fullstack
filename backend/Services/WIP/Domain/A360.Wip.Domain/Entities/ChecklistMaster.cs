
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Wip.Domain.Entities;

public class ChecklistMaster : BaseEntity
{
    [BsonElement("checklist_id")]
    public string ChecklistId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string? AssetId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("checklist_name")]
    public string ChecklistName { get; set; } = null!;

    [BsonElement("checklist_type")]
    public string? ChecklistType { get; set; }

    [BsonElement("applicable_work_type")]
    public string? ApplicableWorkType { get; set; }

    [BsonElement("version_number")]
    public int? VersionNumber { get; set; }

    [BsonElement("is_mandatory")]
    public bool IsMandatory { get; set; }
}
