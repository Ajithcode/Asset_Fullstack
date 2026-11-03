
using A360.Maintenance.Api.Contracts;

namespace A360.Maintenance.Api.Validation;

public static class TechnicianValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateTechnicianRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.TechnicianId))
        {
            errors["TechnicianId"] =
                ["TechnicianId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["Name"] =
                ["Name is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateTechnicianRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.TechnicianId))
        {
            errors["TechnicianId"] =
                ["TechnicianId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["Name"] =
                ["Name is required"];
        }

        return errors;
    }
}
