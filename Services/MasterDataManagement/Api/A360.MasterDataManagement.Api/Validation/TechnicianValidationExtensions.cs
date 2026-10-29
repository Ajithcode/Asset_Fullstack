using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class TechnicianValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateTechnicianRequest request)
    {
        return ValidateFields(
            request.TechnicianName, request.TechnicianId, request.AssignedTeam, request.Email, request.PhoneNumber);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateTechnicianRequest request)
    {
        return ValidateFields(
            request.TechnicianName, request.TechnicianId, request.AssignedTeam, request.Email, request.PhoneNumber);
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
        string? phoneNumber)
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

        ContactValidation.ValidateEmailAndPhone(errors, email, phoneNumber);

        return errors;
    }
}
