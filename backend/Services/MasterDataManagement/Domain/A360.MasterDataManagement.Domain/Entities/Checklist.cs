using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class Checklist : BaseEntity
{
    [BsonElement("checklist_name")]
    public string ChecklistName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // e.g. "Inspection" or "Maintenance".
    [BsonElement("checklist_type")]
    public string ChecklistType { get; set; } = null!;

    // e.g. "HVAC" or "Fire Safety".
    [BsonElement("category")]
    public string Category { get; set; } = null!;

    [BsonElement("description")]
    public string? Description { get; set; }

    // The items to check, in order.
    [BsonElement("items")]
    public List<MasterDataStep> Items { get; set; } = [];
}
