using A360.Inspection.Api.Contracts;

namespace A360.Inspection.Api.Validation;

public static class InspectionTaskValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateInspectionTaskRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.TaskTitle))
        {
            errors["TaskTitle"] = ["TaskTitle is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateInspectionTaskRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.TaskTitle))
        {
            errors["TaskTitle"] = ["TaskTitle is required"];
        }

        return errors;
    }
}
