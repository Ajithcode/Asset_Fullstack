using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class TaskTemplate : BaseEntity
{
    [BsonElement("template_name")]
    public string TemplateName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Points at a maintenance type in this service.
    [BsonElement("maintenance_type")]
    public MasterDataReference? MaintenanceType { get; set; }

    // In hours, e.g. 2.5.
    [BsonElement("estimated_duration")]
    public double? EstimatedDuration { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // The tasks to carry out, in order.
    [BsonElement("tasks")]
    public List<MasterDataStep> Tasks { get; set; } = [];
}
