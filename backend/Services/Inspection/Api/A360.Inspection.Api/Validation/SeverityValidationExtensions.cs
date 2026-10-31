using A360.Inspection.Api.Contracts;

namespace A360.Inspection.Api.Validation;

public static class SeverityValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateSeverityRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.SeverityName))
        {
            errors["SeverityName"] = ["SeverityName is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateSeverityRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.SeverityName))
        {
            errors["SeverityName"] = ["SeverityName is required"];
        }

        return errors;
    }
}
