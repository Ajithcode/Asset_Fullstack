using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class EmployeeValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateEmployeeRequest request)
    {
        return ValidateFields(
            request.EmployeeName, request.EmployeeId, request.Department, request.Email, request.PhoneNumber,
            request.ReportingManager, request.AlternatePhone, request.EmergencyContactPhone, request.Certifications);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateEmployeeRequest request)
    {
        return ValidateFields(
            request.EmployeeName, request.EmployeeId, request.Department, request.Email, request.PhoneNumber,
            request.ReportingManager, request.AlternatePhone, request.EmergencyContactPhone, request.Certifications);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateEmployeeStatusRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            errors["Status"] =
                ["Status is required"];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateFields(
        string? employeeName,
        string? employeeId,
        MasterDataReferenceDto? department,
        string? email,
        string? phoneNumber,
        MasterDataReferenceDto? reportingManager,
        string? alternatePhone,
        string? emergencyContactPhone,
        List<PersonCertificationDto>? certifications)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(employeeName))
        {
            errors["EmployeeName"] =
                ["EmployeeName is required"];
        }

        if (string.IsNullOrWhiteSpace(employeeId))
        {
            errors["EmployeeId"] =
                ["EmployeeId is required"];
        }

        // The department is a record in this service and is normally picked by id; a name alone is kept as typed.
        if (department is not null && string.IsNullOrWhiteSpace(department.Id) && string.IsNullOrWhiteSpace(department.Name))
        {
            errors["Department"] =
                ["Department must have a department id or name"];
        }

        // The reporting manager is another employee, so it is picked by id.
        if (reportingManager is not null && string.IsNullOrWhiteSpace(reportingManager.Id))
        {
            errors["ReportingManager"] =
                ["ReportingManager must have an employee id"];
        }

        ContactValidation.ValidateEmailAndPhone(errors, email, phoneNumber);
        ContactValidation.ValidateEmailAndPhone(errors, null, alternatePhone, "AlternatePhone");
        ContactValidation.ValidateEmailAndPhone(errors, null, emergencyContactPhone, "EmergencyContactPhone");

        if (certifications is not null && certifications.Any(x => x is null || string.IsNullOrWhiteSpace(x.Name)))
        {
            errors["Certifications"] =
                ["Every certification needs a name"];
        }

        return errors;
    }
}
