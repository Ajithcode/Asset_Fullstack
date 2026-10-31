using A360.Inspection.Api.Contracts;

namespace A360.Inspection.Api.Validation;

public static class PriorityValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreatePriorityRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.PriorityName))
        {
            errors["PriorityName"] = ["PriorityName is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdatePriorityRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.PriorityName))
        {
            errors["PriorityName"] = ["PriorityName is required"];
        }

        return errors;
    }
}
