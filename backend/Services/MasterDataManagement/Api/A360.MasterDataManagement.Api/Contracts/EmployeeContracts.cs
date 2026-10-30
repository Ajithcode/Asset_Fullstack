using A360.MasterDataManagement.Domain.ValueObjects;
using EmployeeEntity = A360.MasterDataManagement.Domain.Entities.Employee;

namespace A360.MasterDataManagement.Api.Contracts;

// Dates are sent and returned as "yyyy-MM-dd".
public sealed record PersonCertificationDto(
    string? Name,
    DateOnly? Expiry)
{
    public PersonCertification ToValue()
    {
        return new PersonCertification
        {
            Name = Name?.Trim() ?? string.Empty,
            Expiry = Expiry
        };
    }

    public static PersonCertificationDto FromValue(PersonCertification certification)
    {
        return new PersonCertificationDto(certification.Name, certification.Expiry);
    }

    public static List<PersonCertification> ToValues(IEnumerable<PersonCertificationDto>? certifications)
    {
        return certifications?.Select(x => x.ToValue()).ToList() ?? [];
    }

    public static IReadOnlyList<PersonCertificationDto> FromValues(IEnumerable<PersonCertification>? certifications)
    {
        return certifications?.Select(FromValue).ToList() ?? [];
    }
}

public sealed record CreateEmployeeRequest(
    string? EmployeeName,
    string? EmployeeId,
    MasterDataReferenceDto? Department,
    string? Designation,
    string? Email,
    string? PhoneNumber,
    string? Photo,
    string? EmploymentType,
    DateOnly? JoiningDate,
    string? Site,
    MasterDataReferenceDto? ReportingManager,
    string? CostCenter,
    string? AlternatePhone,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? Address,
    string? Nationality,
    List<string>? Skills,
    List<PersonCertificationDto>? Certifications,
    string? AccessRole,
    bool? CanApprove,
    bool? NotifyEmail,
    bool? NotifySms,
    string? Status,
    string? CreatedBy,
    string? ClientId,
    string? TenantId)
{
    public EmployeeEntity ToEntity()
    {
        return new EmployeeEntity
        {
            EmployeeName = EmployeeName!.Trim(),
            EmployeeId = EmployeeId!.Trim(),
            Department = Department?.ToReference(),
            Designation = string.IsNullOrWhiteSpace(Designation) ? null : Designation.Trim(),
            Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim(),
            Photo = StructureText.Clean(Photo),
            EmploymentType = StructureText.Clean(EmploymentType),
            JoiningDate = JoiningDate,
            Site = StructureText.Clean(Site),
            ReportingManager = ReportingManager?.ToReference(),
            CostCenter = StructureText.Clean(CostCenter),
            AlternatePhone = StructureText.Clean(AlternatePhone),
            EmergencyContactName = StructureText.Clean(EmergencyContactName),
            EmergencyContactPhone = StructureText.Clean(EmergencyContactPhone),
            Address = StructureText.Clean(Address),
            Nationality = StructureText.Clean(Nationality),
            Skills = StructureText.CleanList(Skills),
            Certifications = PersonCertificationDto.ToValues(Certifications),
            AccessRole = StructureText.Clean(AccessRole),
            CanApprove = CanApprove ?? false,
            NotifyEmail = NotifyEmail ?? false,
            NotifySms = NotifySms ?? false,

            CreatedBy = CreatedBy,
            ClientId = ClientId,
            TenantId = TenantId,
            Status = string.IsNullOrWhiteSpace(Status) ? MasterDataStatuses.Active : Status,

            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}

public sealed record UpdateEmployeeRequest(
    string? EmployeeName,
    string? EmployeeId,
    MasterDataReferenceDto? Department,
    string? Designation,
    string? Email,
    string? PhoneNumber,
    string? Photo,
    string? EmploymentType,
    DateOnly? JoiningDate,
    string? Site,
    MasterDataReferenceDto? ReportingManager,
    string? CostCenter,
    string? AlternatePhone,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? Address,
    string? Nationality,
    List<string>? Skills,
    List<PersonCertificationDto>? Certifications,
    string? AccessRole,
    bool? CanApprove,
    bool? NotifyEmail,
    bool? NotifySms,
    string? Status,
    string? UpdatedBy)
{
    public void ApplyTo(EmployeeEntity employee)
    {
        employee.EmployeeName = EmployeeName!.Trim();
        employee.EmployeeId = EmployeeId!.Trim();
        employee.Department = Department?.ToReference();
        employee.Designation = string.IsNullOrWhiteSpace(Designation) ? null : Designation.Trim();
        employee.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        employee.PhoneNumber = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim();
        employee.Photo = StructureText.Clean(Photo);
        employee.EmploymentType = StructureText.Clean(EmploymentType);
        employee.JoiningDate = JoiningDate;
        employee.Site = StructureText.Clean(Site);
        employee.ReportingManager = ReportingManager?.ToReference();
        employee.CostCenter = StructureText.Clean(CostCenter);
        employee.AlternatePhone = StructureText.Clean(AlternatePhone);
        employee.EmergencyContactName = StructureText.Clean(EmergencyContactName);
        employee.EmergencyContactPhone = StructureText.Clean(EmergencyContactPhone);
        employee.Address = StructureText.Clean(Address);
        employee.Nationality = StructureText.Clean(Nationality);
        employee.Skills = StructureText.CleanList(Skills);
        employee.Certifications = PersonCertificationDto.ToValues(Certifications);
        employee.AccessRole = StructureText.Clean(AccessRole);
        employee.CanApprove = CanApprove ?? false;
        employee.NotifyEmail = NotifyEmail ?? false;
        employee.NotifySms = NotifySms ?? false;

        employee.UpdatedBy = UpdatedBy;
        employee.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(Status))
        {
            employee.Status = Status;
        }
    }
}

public sealed record UpdateEmployeeStatusRequest(
    string? Status,
    string? UpdatedBy);

// Id is the record id; EmployeeId is the organisation's employee number.
public sealed record EmployeeResponse(
    string Id,
    string EmployeeName,
    string EmployeeId,
    MasterDataReferenceDto? Department,
    string? Designation,
    string? Email,
    string? PhoneNumber,
    string? Photo,
    string? EmploymentType,
    DateOnly? JoiningDate,
    string? Site,
    MasterDataReferenceDto? ReportingManager,
    string? CostCenter,
    string? AlternatePhone,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? Address,
    string? Nationality,
    IReadOnlyList<string> Skills,
    IReadOnlyList<PersonCertificationDto> Certifications,
    string? AccessRole,
    bool CanApprove,
    bool NotifyEmail,
    bool NotifySms,
    string? CreatedBy,
    DateTime? CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt,
    string? ClientId,
    string? TenantId,
    string? Status,
    bool IsDeleted)
{
    public static EmployeeResponse FromEntity(EmployeeEntity employee)
    {
        return new EmployeeResponse(
            employee.Id ?? string.Empty,
            employee.EmployeeName ?? string.Empty,
            employee.EmployeeId ?? string.Empty,
            employee.Department is null
                ? null
                : MasterDataReferenceDto.FromReference(employee.Department),
            employee.Designation,
            employee.Email,
            employee.PhoneNumber,
            employee.Photo,
            employee.EmploymentType,
            employee.JoiningDate,
            employee.Site,
            employee.ReportingManager is null
                ? null
                : MasterDataReferenceDto.FromReference(employee.ReportingManager),
            employee.CostCenter,
            employee.AlternatePhone,
            employee.EmergencyContactName,
            employee.EmergencyContactPhone,
            employee.Address,
            employee.Nationality,
            employee.Skills ?? [],
            PersonCertificationDto.FromValues(employee.Certifications),
            employee.AccessRole,
            employee.CanApprove,
            employee.NotifyEmail,
            employee.NotifySms,
            employee.CreatedBy,
            employee.CreatedAt,
            employee.UpdatedBy,
            employee.UpdatedAt,
            employee.ClientId,
            employee.TenantId,
            employee.Status,
            employee.IsDeleted
        );
    }
}
