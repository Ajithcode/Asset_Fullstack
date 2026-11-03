
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Maintenance.Domain.Entities;

public class Technician : BaseEntity
{
    [BsonElement("technician_id")]
    public string TechnicianId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string? AssetId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("skill_set")]
    public List<string>? SkillSet { get; set; }

    [BsonElement("certification")]
    public string? Certification { get; set; }

    [BsonElement("availability")]
    public string? Availability { get; set; }

    [BsonElement("assigned_tasks")]
    public List<string>? AssignedTasks { get; set; }
}
