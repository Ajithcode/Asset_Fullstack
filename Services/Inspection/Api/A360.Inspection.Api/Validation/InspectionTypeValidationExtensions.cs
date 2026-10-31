using A360.Inspection.Api.Contracts;

namespace A360.Inspection.Api.Validation;

public static class InspectionTypeValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateInspectionTypeRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.InspectionTypeName))
        {
            errors["InspectionTypeName"] = ["InspectionTypeName is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateInspectionTypeRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.InspectionTypeName))
        {
            errors["InspectionTypeName"] = ["InspectionTypeName is required"];
        }

        return errors;
    }
}
