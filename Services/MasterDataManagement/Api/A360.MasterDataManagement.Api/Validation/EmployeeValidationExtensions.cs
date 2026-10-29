using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class EmployeeValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateEmployeeRequest request)
    {
        return ValidateFields(
            request.EmployeeName, request.EmployeeId, request.Department, request.Email, request.PhoneNumber);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateEmployeeRequest request)
    {
        return ValidateFields(
            request.EmployeeName, request.EmployeeId, request.Department, request.Email, request.PhoneNumber);
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
        string? phoneNumber)
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

        // The department is a record in this service, so it is picked by id.
        if (department is not null && string.IsNullOrWhiteSpace(department.Id))
        {
            errors["Department"] =
                ["Department must have a department id"];
        }

        ContactValidation.ValidateEmailAndPhone(errors, email, phoneNumber);

        return errors;
    }
}
