using A360.Inspection.Api.Contracts;

namespace A360.Inspection.Api.Validation;

public static class NumberingSequenceValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateNumberingSequenceRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.NumberType))
        {
            errors["NumberType"] = ["NumberType is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateNumberingSequenceRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.NumberType))
        {
            errors["NumberType"] = ["NumberType is required"];
        }

        return errors;
    }
}
