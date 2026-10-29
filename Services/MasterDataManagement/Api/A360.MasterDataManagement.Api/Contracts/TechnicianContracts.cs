using TechnicianEntity = A360.MasterDataManagement.Domain.Entities.Technician;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateTechnicianRequest(
    string? TechnicianName,
    string? TechnicianId,
    string? PrimarySkill,
    MasterDataReferenceDto? AssignedTeam,
    string? Email,
    string? PhoneNumber,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public TechnicianEntity ToEntity()
    {
        return new TechnicianEntity
        {
            TechnicianName = TechnicianName!.Trim(),
            TechnicianId = TechnicianId!.Trim(),
            PrimarySkill = string.IsNullOrWhiteSpace(PrimarySkill) ? null : PrimarySkill.Trim(),
            AssignedTeam = AssignedTeam?.ToReference(),
            Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim(),

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateTechnicianRequest(
    string? TechnicianName,
    string? TechnicianId,
    string? PrimarySkill,
    MasterDataReferenceDto? AssignedTeam,
    string? Email,
    string? PhoneNumber,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(TechnicianEntity technician)
    {
        technician.TechnicianName = TechnicianName!.Trim();
        technician.TechnicianId = TechnicianId!.Trim();
        technician.PrimarySkill = string.IsNullOrWhiteSpace(PrimarySkill) ? null : PrimarySkill.Trim();
        technician.AssignedTeam = AssignedTeam?.ToReference();
        technician.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        technician.PhoneNumber = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim();

        technician.UpdatedBy = UpdatedBy;
        technician.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            technician.Status = Status;
        }
    }
}

public sealed record UpdateTechnicianStatusRequest(
    string? Status,
    string? UpdatedBy);

// Id is the record id; TechnicianId is the organisation's technician number.
public sealed record TechnicianResponse(
    string Id,
    string TechnicianName,
    string TechnicianId,
    string? PrimarySkill,
    MasterDataReferenceDto? AssignedTeam,
    string? Email,
    string? PhoneNumber,
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
            technician.TechnicianName ?? string.Empty,
            technician.TechnicianId ?? string.Empty,
            technician.PrimarySkill,
            technician.AssignedTeam is null
                ? null
                : MasterDataReferenceDto.FromReference(technician.AssignedTeam),
            technician.Email,
            technician.PhoneNumber,
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
