
using A360.Maintenance.Api.Contracts;

namespace A360.Maintenance.Api.Validation;

public static class ComplianceInspectionValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateComplianceInspectionRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.InspectionId))
        {
            errors["InspectionId"] =
                ["InspectionId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.InspectionType))
        {
            errors["InspectionType"] =
                ["InspectionType is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateComplianceInspectionRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.InspectionId))
        {
            errors["InspectionId"] =
                ["InspectionId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.InspectionType))
        {
            errors["InspectionType"] =
                ["InspectionType is required"];
        }

        return errors;
    }
}
