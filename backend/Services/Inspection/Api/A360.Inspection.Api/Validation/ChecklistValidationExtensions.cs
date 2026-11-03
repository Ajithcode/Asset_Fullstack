using A360.Inspection.Api.Contracts;

namespace A360.Inspection.Api.Validation;

public static class ChecklistValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateChecklistRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.TemplateName))
        {
            errors["TemplateName"] = ["TemplateName is required"];
        }

        if (string.IsNullOrWhiteSpace(request.InspectionTypeId))
        {
            errors["InspectionTypeId"] = ["InspectionTypeId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.AssetCategory))
        {
            errors["AssetCategory"] = ["AssetCategory is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateChecklistRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.TemplateName))
        {
            errors["TemplateName"] = ["TemplateName is required"];
        }

        if (string.IsNullOrWhiteSpace(request.InspectionTypeId))
        {
            errors["InspectionTypeId"] = ["InspectionTypeId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.AssetCategory))
        {
            errors["AssetCategory"] = ["AssetCategory is required"];
        }

        return errors;
    }
}
