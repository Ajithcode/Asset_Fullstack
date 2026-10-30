
using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.Wip.Domain.Entities;

public class ResourceMaster : BaseEntity
{
    [BsonElement("resource_id")]
    public string ResourceId { get; set; } = null!;

    [BsonElement("asset_id")]
    public string? AssetId { get; set; }

    [BsonElement("asset_name")]
    public string? AssetName { get; set; }

    [BsonElement("resource_name")]
    public string ResourceName { get; set; } = null!;

    [BsonElement("resource_type")]
    public string? ResourceType { get; set; }

    [BsonElement("skill_set")]
    public List<string> SkillSet { get; set; } = [];

    [BsonElement("department_id")]
    public string? DepartmentId { get; set; }

    [BsonElement("contact_number")]
    public string? ContactNumber { get; set; }

    [BsonElement("email")]
    public string? Email { get; set; }

    [BsonElement("availability_status")]
    public string? AvailabilityStatus { get; set; }

    [BsonElement("shift_id")]
    public string? ShiftId { get; set; }

    [BsonElement("cost_per_hour")]
    public double? CostPerHour { get; set; }

    [BsonElement("certification_details")]
    public string? CertificationDetails { get; set; }
}
