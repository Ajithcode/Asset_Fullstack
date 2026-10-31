
using A360.Maintenance.Api.Contracts;

namespace A360.Maintenance.Api.Validation;

public static class CostTrackingValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateCostTrackingRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateCostTrackingRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        return errors;
    }
}
