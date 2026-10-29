using EmployeeEntity = A360.MasterDataManagement.Domain.Entities.Employee;

namespace A360.MasterDataManagement.Api.Contracts;

public sealed record CreateEmployeeRequest(
    string? EmployeeName,
    string? EmployeeId,
    MasterDataReferenceDto? Department,
    string? Designation,
    string? Email,
    string? PhoneNumber,
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
