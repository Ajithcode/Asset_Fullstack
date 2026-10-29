using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class DepartmentValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateDepartmentRequest request)
    {
        return ValidateFields(
            request.DepartmentName,
            request.Code,
            request.BusinessUnit,
            request.DepartmentHead,
            request.Employees,
            request.Assets);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateDepartmentRequest request)
    {
        return ValidateFields(
            request.DepartmentName,
            request.Code,
            request.BusinessUnit,
            request.DepartmentHead,
            request.Employees,
            request.Assets);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateDepartmentStatusRequest request)
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
        string? departmentName,
        string? code,
        MasterDataReferenceDto? businessUnit,
        MasterDataReferenceDto? departmentHead,
        List<MasterDataReferenceDto>? employees,
        List<MasterDataReferenceDto>? assets)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(departmentName))
        {
            errors["DepartmentName"] =
                ["DepartmentName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        // The business unit is a record in this service, so it is picked by id and its name filled in.
        if (businessUnit is not null && string.IsNullOrWhiteSpace(businessUnit.Id))
        {
            errors["BusinessUnit"] =
                ["BusinessUnit must have a business unit id"];
        }

        // The head is normally an employee picked by id (its name is filled in); a name alone is also accepted.
        if (departmentHead is not null
            && string.IsNullOrWhiteSpace(departmentHead.Id)
            && string.IsNullOrWhiteSpace(departmentHead.Name))
        {
            errors["DepartmentHead"] =
                ["DepartmentHead needs an employee id or a name"];
        }

        // Employees are records in this service, so they are picked by id.
        if (employees is not null)
        {
            if (employees.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id)))
            {
                errors["Employees"] =
                    ["Every entry in Employees needs an employee id"];
            }
            else if (employees.GroupBy(x => x.Id!.Trim()).Any(x => x.Count() > 1))
            {
                errors["Employees"] =
                    ["Employees contains duplicate entries"];
            }
        }
        MasterDataReferenceValidation.ValidateList(errors, "Assets", assets);

        return errors;
    }
}
