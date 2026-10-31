using A360.Inspection.Api.Contracts;

namespace A360.Inspection.Api.Validation;

public static class InspectionPlanValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateInspectionPlanRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.PlanName))
        {
            errors["PlanName"] = ["PlanName is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateInspectionPlanRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.PlanName))
        {
            errors["PlanName"] = ["PlanName is required"];
        }

        return errors;
    }
}
