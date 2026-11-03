
using TechnicianEntity = A360.Maintenance.Domain.Entities.Technician;

namespace A360.Maintenance.Api.Contracts;

public sealed record CreateTechnicianRequest(
    string? TechnicianId,
    string? AssetId,
    string? AssetName,
    string? Name,
    List<string>? SkillSet,
    string? Certification,
    string? Availability,
    List<string>? AssignedTasks,
    string? CreatedBy,
    string? ClientId,
    string? TenantId,
    string? Status)
{
    public TechnicianEntity ToEntity()
    {
        return new TechnicianEntity
        {
            TechnicianId = TechnicianId,
            AssetId = AssetId,
            AssetName = AssetName,
            Name = Name,
            SkillSet = SkillSet ?? new List<string>(),
            Certification = Certification,
            Availability = Availability,
            AssignedTasks = AssignedTasks ?? new List<string>(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateTechnicianRequest(
    string? TechnicianId,
    string? AssetId,
    string? AssetName,
    string? Name,
    List<string>? SkillSet,
    string? Certification,
    string? Availability,
    List<string>? AssignedTasks,
    string? UpdatedBy,
    string? Status)
{
    public void ApplyTo(TechnicianEntity technician)
    {
        technician.TechnicianId = TechnicianId;
        technician.AssetId = AssetId;
        technician.AssetName = AssetName;
        technician.Name = Name;
        technician.SkillSet = SkillSet ?? new List<string>();
        technician.Certification = Certification;
        technician.Availability = Availability;
        technician.AssignedTasks = AssignedTasks ?? new List<string>();

        technician.UpdatedBy = UpdatedBy;
        technician.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            technician.Status = Status;
        }
    }
}

public sealed record TechnicianResponse(
    string Id,
    string TechnicianId,
    string? AssetId,
    string? AssetName,
    string Name,
    List<string>? SkillSet,
    string? Certification,
    string? Availability,
    List<string>? AssignedTasks,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static TechnicianResponse FromEntity(TechnicianEntity technician)
    {
        return new TechnicianResponse(
            technician.Id ?? string.Empty,
            technician.TechnicianId ?? string.Empty,
            technician.AssetId,
            technician.AssetName,
            technician.Name ?? string.Empty,
            technician.SkillSet,
            technician.Certification,
            technician.Availability,
            technician.AssignedTasks,
            technician.CreatedBy,
            technician.CreatedAt,
            technician.UpdatedBy,
            technician.UpdatedAt,
            technician.ClientId,
            technician.TenantId,
            technician.Status,
            technician.IsDeleted
        );
    }
}
