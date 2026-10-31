using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class TeamValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateTeamRequest request)
    {
        return ValidateFields(request.TeamName, request.Code, request.Department, request.TeamLeader, request.Members,
            request.EmployeeMembers, request.Shift);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateTeamRequest request)
    {
        return ValidateFields(request.TeamName, request.Code, request.Department, request.TeamLeader, request.Members,
            request.EmployeeMembers, request.Shift);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateTeamStatusRequest request)
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
        string? teamName,
        string? code,
        MasterDataReferenceDto? department,
        MasterDataReferenceDto? teamLeader,
        List<MasterDataReferenceDto>? members,
        List<MasterDataReferenceDto>? employeeMembers,
        MasterDataReferenceDto? shift)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(teamName))
        {
            errors["TeamName"] =
                ["TeamName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        // The department is normally picked by id; a name alone is kept as typed.
        if (department is not null && string.IsNullOrWhiteSpace(department.Id) && string.IsNullOrWhiteSpace(department.Name))
        {
            errors["Department"] =
                ["Department must have a department id or name"];
        }

        // Leader, members and shift are records in this service, so they are picked by id.
        if (teamLeader is not null && string.IsNullOrWhiteSpace(teamLeader.Id))
        {
            errors["TeamLeader"] =
                ["TeamLeader must have a technician or employee id"];
        }

        if (shift is not null && string.IsNullOrWhiteSpace(shift.Id))
        {
            errors["Shift"] =
                ["Shift must have a shift id"];
        }

        if (employeeMembers is not null)
        {
            if (employeeMembers.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id)))
            {
                errors["EmployeeMembers"] =
                    ["Every entry in EmployeeMembers needs an employee id"];
            }
            else if (employeeMembers.GroupBy(x => x.Id!.Trim()).Any(x => x.Count() > 1))
            {
                errors["EmployeeMembers"] =
                    ["EmployeeMembers contains duplicate entries"];
            }
        }

        if (members is not null)
        {
            if (members.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id)))
            {
                errors["Members"] =
                    ["Every entry in Members needs a technician id"];
            }
            else if (members.GroupBy(x => x.Id!.Trim()).Any(x => x.Count() > 1))
            {
                errors["Members"] =
                    ["Members contains duplicate entries"];
            }
        }

        return errors;
    }
}
