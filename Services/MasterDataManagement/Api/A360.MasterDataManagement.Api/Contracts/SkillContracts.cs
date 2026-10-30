using SkillEntity = A360.MasterDataManagement.Domain.Entities.Skill;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateSkillRequest(
    string? SkillName,
    string? Code,
    string? Category,
    string? Type,
    string? Description,
    string? Icon,
    List<string>? Levels,
    List<string>? RelatedRoles,
    bool? CertificationRequired,
    int? CertificationValidityMonths,
    bool? AssessmentRequired,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public SkillEntity ToEntity()
    {
        return new SkillEntity
        {
            SkillName = SkillName!.Trim(),
            Code = Code!.Trim(),
            Category = string.IsNullOrWhiteSpace(Category) ? null : Category.Trim(),
            Type = string.IsNullOrWhiteSpace(Type) ? null : Type.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Icon = string.IsNullOrWhiteSpace(Icon) ? null : Icon.Trim(),
            Levels = StructureText.CleanList(Levels),
            RelatedRoles = StructureText.CleanList(RelatedRoles),
            CertificationRequired = CertificationRequired ?? false,
            CertificationValidityMonths = CertificationValidityMonths,
            AssessmentRequired = AssessmentRequired ?? false,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateSkillRequest(
    string? SkillName,
    string? Code,
    string? Category,
    string? Type,
    string? Description,
    string? Icon,
    List<string>? Levels,
    List<string>? RelatedRoles,
    bool? CertificationRequired,
    int? CertificationValidityMonths,
    bool? AssessmentRequired,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(SkillEntity skill)
    {
        skill.SkillName = SkillName!.Trim();
        skill.Code = Code!.Trim();
        skill.Category = string.IsNullOrWhiteSpace(Category) ? null : Category.Trim();
        skill.Type = string.IsNullOrWhiteSpace(Type) ? null : Type.Trim();
        skill.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        skill.Icon = string.IsNullOrWhiteSpace(Icon) ? null : Icon.Trim();
        skill.Levels = StructureText.CleanList(Levels);
        skill.RelatedRoles = StructureText.CleanList(RelatedRoles);
        skill.CertificationRequired = CertificationRequired ?? false;
        skill.CertificationValidityMonths = CertificationValidityMonths;
        skill.AssessmentRequired = AssessmentRequired ?? false;

        skill.UpdatedBy = UpdatedBy;
        skill.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            skill.Status = Status;
        }
    }
}

public sealed record UpdateSkillStatusRequest(
    string? Status,
    string? UpdatedBy);

public sealed record SkillResponse(
    string Id,
    string SkillName,
    string Code,
    string? Category,
    string? Type,
    string? Description,
    string? Icon,
    IReadOnlyList<string> Levels,
    IReadOnlyList<string> RelatedRoles,
    bool CertificationRequired,
    int? CertificationValidityMonths,
    bool AssessmentRequired,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static SkillResponse FromEntity(SkillEntity skill)
    {
        return new SkillResponse(
            skill.Id ?? string.Empty,
            skill.SkillName ?? string.Empty,
            skill.Code ?? string.Empty,
            skill.Category,
            skill.Type,
            skill.Description,
            skill.Icon,
            skill.Levels ?? [],
            skill.RelatedRoles ?? [],
            skill.CertificationRequired,
            skill.CertificationValidityMonths,
            skill.AssessmentRequired,
            skill.CreatedBy,
            skill.CreatedAt,
            skill.UpdatedBy,
            skill.UpdatedAt,
            skill.ClientId,
            skill.TenantId,
            skill.Status,
            skill.IsDeleted
        );
    }
}
