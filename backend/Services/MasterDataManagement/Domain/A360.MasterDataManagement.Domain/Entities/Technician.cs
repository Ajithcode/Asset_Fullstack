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

    // Profile photo as an image URL or data URL.
    [BsonElement("photo")]
    public string? Photo { get; set; }

    [BsonElement("skill_level")]
    public string? SkillLevel { get; set; }

    // Skill names from the skill master, besides the primary skill.
    [BsonElement("secondary_skills")]
    public List<string> SecondarySkills { get; set; } = [];

    [BsonElement("certifications")]
    public List<PersonCertification> Certifications { get; set; } = [];

    // Points at the employee record of an in-house technician.
    [BsonElement("employee")]
    public MasterDataReference? Employee { get; set; }

    // "In-house" or "Contractor".
    [BsonElement("source")]
    public string? Source { get; set; }

    [BsonElement("employment_type")]
    public string? EmploymentType { get; set; }

    [BsonElement("joining_date")]
    public DateOnly? JoiningDate { get; set; }

    [BsonElement("hourly_rate")]
    public decimal? HourlyRate { get; set; }

    [BsonElement("emergency_contact_name")]
    public string? EmergencyContactName { get; set; }

    [BsonElement("emergency_contact_phone")]
    public string? EmergencyContactPhone { get; set; }

    [BsonElement("address")]
    public string? Address { get; set; }

    [BsonElement("available_for_dispatch")]
    public bool AvailableForDispatch { get; set; }

    [BsonElement("mobile_app_access")]
    public bool MobileAppAccess { get; set; }

    [BsonElement("max_open_work_orders")]
    public int MaxOpenWorkOrders { get; set; }
}
