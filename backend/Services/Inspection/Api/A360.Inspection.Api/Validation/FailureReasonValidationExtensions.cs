using A360.Inspection.Api.Contracts;

namespace A360.Inspection.Api.Validation;

public static class FailureReasonValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateFailureReasonRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.FailureReasonName))
        {
            errors["FailureReasonName"] = ["FailureReasonName is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateFailureReasonRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.FailureReasonName))
        {
            errors["FailureReasonName"] = ["FailureReasonName is required"];
        }

        return errors;
    }
}
