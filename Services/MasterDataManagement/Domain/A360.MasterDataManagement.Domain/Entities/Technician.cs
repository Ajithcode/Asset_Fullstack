using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;
using A360.MasterDataManagement.Domain.ValueObjects;

namespace A360.MasterDataManagement.Domain.Entities;

// Ignores the plain-text "assigned_team" older records still carry: the team is now a link to a Team record.
[BsonIgnoreExtraElements]
public class Technician : BaseEntity
{
    [BsonElement("technician_name")]
    public string TechnicianName { get; set; } = null!;

    // The organisation's own technician number, e.g. "TECH-0042"; not the record id.
    [BsonElement("technician_id")]
    public string TechnicianId { get; set; } = null!;

    [BsonElement("primary_skill")]
    public string? PrimarySkill { get; set; }

    // Points at a team in this service. This is what a team's member list is built from.
    [BsonElement("team")]
    public MasterDataReference? AssignedTeam { get; set; }

    [BsonElement("email")]
    public string? Email { get; set; }

    [BsonElement("phone_number")]
    public string? PhoneNumber { get; set; }
}
