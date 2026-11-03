using A360.Inspection.Api.Contracts;

namespace A360.Inspection.Api.Validation;

public static class DefectValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateDefectRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.DefectName))
        {
            errors["DefectName"] = ["DefectName is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateDefectRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.DefectName))
        {
            errors["DefectName"] = ["DefectName is required"];
        }

        return errors;
    }
}
