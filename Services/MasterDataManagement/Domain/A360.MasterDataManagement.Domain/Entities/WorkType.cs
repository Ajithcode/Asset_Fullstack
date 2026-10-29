using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

public class WorkType : BaseEntity
{
    [BsonElement("work_type_name")]
    public string WorkTypeName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Points at a maintenance type in this service.
    [BsonElement("maintenance_type")]
    public MasterDataReference? MaintenanceType { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // In hours, e.g. 1.5.
    [BsonElement("estimated_duration")]
    public double? EstimatedDuration { get; set; }
}
