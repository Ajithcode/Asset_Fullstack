using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

// A team's members are not stored here: each technician points at its assigned team.
public class Team : BaseEntity
{
    [BsonElement("team_name")]
    public string TeamName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    // Points at a department in this service.
    [BsonElement("department")]
    public MasterDataReference? Department { get; set; }

    // Points at a technician in this service.
    [BsonElement("team_leader")]
    public MasterDataReference? TeamLeader { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }
}
