using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class TechnicianValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateTechnicianRequest request)
    {
        return ValidateFields(
            request.TechnicianName, request.TechnicianId, request.AssignedTeam, request.Email, request.PhoneNumber,
            request.Employee, request.Certifications, request.HourlyRate, request.MaxOpenWorkOrders, request.EmergencyContactPhone);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateTechnicianRequest request)
    {
        return ValidateFields(
            request.TechnicianName, request.TechnicianId, request.AssignedTeam, request.Email, request.PhoneNumber,
            request.Employee, request.Certifications, request.HourlyRate, request.MaxOpenWorkOrders, request.EmergencyContactPhone);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateTechnicianStatusRequest request)
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
        string? technicianName,
        string? technicianId,
        MasterDataReferenceDto? assignedTeam,
        string? email,
        string? phoneNumber,
        MasterDataReferenceDto? employee,
        List<PersonCertificationDto>? certifications,
        decimal? hourlyRate,
        int? maxOpenWorkOrders,
        string? emergencyContactPhone)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(technicianName))
        {
            errors["TechnicianName"] =
                ["TechnicianName is required"];
        }

        if (string.IsNullOrWhiteSpace(technicianId))
        {
            errors["TechnicianId"] =
                ["TechnicianId is required"];
        }

        // The team is a record in this service, so it is picked by id.
        if (assignedTeam is not null && string.IsNullOrWhiteSpace(assignedTeam.Id))
        {
            errors["AssignedTeam"] =
                ["AssignedTeam must have a team id"];
        }

        // The linked employee is a record in this service, so it is picked by id.
        if (employee is not null && string.IsNullOrWhiteSpace(employee.Id))
        {
            errors["Employee"] =
                ["Employee must have an employee id"];
        }

        ContactValidation.ValidateEmailAndPhone(errors, email, phoneNumber);
        ContactValidation.ValidateEmailAndPhone(errors, null, emergencyContactPhone, "EmergencyContactPhone");

        if (certifications is not null && certifications.Any(x => x is null || string.IsNullOrWhiteSpace(x.Name)))
        {
            errors["Certifications"] =
                ["Every certification needs a name"];
        }

        if (hourlyRate < 0)
        {
            errors["HourlyRate"] =
                ["HourlyRate cannot be negative"];
        }

        if (maxOpenWorkOrders is < 1 or > 50)
        {
            errors["MaxOpenWorkOrders"] =
                ["MaxOpenWorkOrders must be between 1 and 50"];
        }

        return errors;
    }
}
