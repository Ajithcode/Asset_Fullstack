using MongoDB.Bson.Serialization.Attributes;
using A360.Domain.Entities;

namespace A360.MasterDataManagement.Domain.Entities;

public class Skill : BaseEntity
{
    [BsonElement("skill_name")]
    public string SkillName { get; set; } = null!;

    [BsonElement("code")]
    public string Code { get; set; } = null!;

    [BsonElement("category")]
    public string? Category { get; set; }

    [BsonElement("type")]
    public string? Type { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    // An icon name or an image URL (e.g. from /api/media/upload), stored as given.
    [BsonElement("icon")]
    public string? Icon { get; set; }

    // Proficiency levels from lowest to highest; empty when the skill is not graded.
    [BsonElement("levels")]
    public List<string> Levels { get; set; } = [];

    // Designations that normally need this skill.
    [BsonElement("related_roles")]
    public List<string> RelatedRoles { get; set; } = [];

    [BsonElement("certification_required")]
    public bool CertificationRequired { get; set; }

    [BsonElement("certification_validity_months")]
    public int? CertificationValidityMonths { get; set; }

    [BsonElement("assessment_required")]
    public bool AssessmentRequired { get; set; }
}
