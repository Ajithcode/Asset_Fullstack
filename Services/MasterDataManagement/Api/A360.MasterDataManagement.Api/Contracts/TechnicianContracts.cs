using TechnicianEntity = A360.MasterDataManagement.Domain.Entities.Technician;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateTechnicianRequest(
    string? TechnicianName,
    string? TechnicianId,
    string? PrimarySkill,
    MasterDataReferenceDto? AssignedTeam,
    string? Email,
    string? PhoneNumber,
    string? Photo,
    string? SkillLevel,
    List<string>? SecondarySkills,
    List<PersonCertificationDto>? Certifications,
    MasterDataReferenceDto? Employee,
    string? Source,
    string? EmploymentType,
    DateOnly? JoiningDate,
    decimal? HourlyRate,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? Address,
    bool? AvailableForDispatch,
    bool? MobileAppAccess,
    int? MaxOpenWorkOrders,
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
            Photo = StructureText.Clean(Photo),
            SkillLevel = StructureText.Clean(SkillLevel),
            SecondarySkills = StructureText.CleanList(SecondarySkills),
            Certifications = PersonCertificationDto.ToValues(Certifications),
            Employee = Employee?.ToReference(),
            Source = StructureText.Clean(Source),
            EmploymentType = StructureText.Clean(EmploymentType),
            JoiningDate = JoiningDate,
            HourlyRate = HourlyRate,
            EmergencyContactName = StructureText.Clean(EmergencyContactName),
            EmergencyContactPhone = StructureText.Clean(EmergencyContactPhone),
            Address = StructureText.Clean(Address),
            AvailableForDispatch = AvailableForDispatch ?? false,
            MobileAppAccess = MobileAppAccess ?? false,
            MaxOpenWorkOrders = MaxOpenWorkOrders ?? TechnicianDefaults.MaxOpenWorkOrders,

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
    string? Photo,
    string? SkillLevel,
    List<string>? SecondarySkills,
    List<PersonCertificationDto>? Certifications,
    MasterDataReferenceDto? Employee,
    string? Source,
    string? EmploymentType,
    DateOnly? JoiningDate,
    decimal? HourlyRate,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? Address,
    bool? AvailableForDispatch,
    bool? MobileAppAccess,
    int? MaxOpenWorkOrders,
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
        technician.Photo = StructureText.Clean(Photo);
        technician.SkillLevel = StructureText.Clean(SkillLevel);
        technician.SecondarySkills = StructureText.CleanList(SecondarySkills);
        technician.Certifications = PersonCertificationDto.ToValues(Certifications);
        technician.Employee = Employee?.ToReference();
        technician.Source = StructureText.Clean(Source);
        technician.EmploymentType = StructureText.Clean(EmploymentType);
        technician.JoiningDate = JoiningDate;
        technician.HourlyRate = HourlyRate;
        technician.EmergencyContactName = StructureText.Clean(EmergencyContactName);
        technician.EmergencyContactPhone = StructureText.Clean(EmergencyContactPhone);
        technician.Address = StructureText.Clean(Address);
        technician.AvailableForDispatch = AvailableForDispatch ?? false;
        technician.MobileAppAccess = MobileAppAccess ?? false;
        technician.MaxOpenWorkOrders = MaxOpenWorkOrders ?? TechnicianDefaults.MaxOpenWorkOrders;

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
    string? Photo,
    string? SkillLevel,
    IReadOnlyList<string> SecondarySkills,
    IReadOnlyList<PersonCertificationDto> Certifications,
    MasterDataReferenceDto? Employee,
    string? Source,
    string? EmploymentType,
    DateOnly? JoiningDate,
    decimal? HourlyRate,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? Address,
    bool AvailableForDispatch,
    bool MobileAppAccess,
    int MaxOpenWorkOrders,
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
            technician.Photo,
            technician.SkillLevel,
            technician.SecondarySkills ?? [],
            PersonCertificationDto.FromValues(technician.Certifications),
            technician.Employee is null ? null : MasterDataReferenceDto.FromReference(technician.Employee),
            technician.Source,
            technician.EmploymentType,
            technician.JoiningDate,
            technician.HourlyRate,
            technician.EmergencyContactName,
            technician.EmergencyContactPhone,
            technician.Address,
            technician.AvailableForDispatch,
            technician.MobileAppAccess,
            technician.MaxOpenWorkOrders,
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

public static class TechnicianDefaults
{
    public const int MaxOpenWorkOrders = 5;
}
