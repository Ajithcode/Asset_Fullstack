
using A360.Maintenance.Api.Contracts;

namespace A360.Maintenance.Api.Validation;

public static class PredictiveMaintenanceValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreatePredictiveMaintenanceRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.SensorType))
        {
            errors["SensorType"] =
                ["SensorType is required"];
        }

        if (string.IsNullOrWhiteSpace(request.RiskLevel))
        {
            errors["RiskLevel"] =
                ["RiskLevel is required"];
        }

        if (request.HealthScore is < 0 or > 100)
        {
            errors["HealthScore"] = ["HealthScore must be between 0 and 100"];
        }

        if (request.Confidence is < 0 or > 100)
        {
            errors["Confidence"] = ["Confidence must be between 0 and 100"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdatePredictiveMaintenanceRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.SensorType))
        {
            errors["SensorType"] =
                ["SensorType is required"];
        }

        if (string.IsNullOrWhiteSpace(request.RiskLevel))
        {
            errors["RiskLevel"] =
                ["RiskLevel is required"];
        }

        if (request.HealthScore is < 0 or > 100)
        {
            errors["HealthScore"] = ["HealthScore must be between 0 and 100"];
        }

        if (request.Confidence is < 0 or > 100)
        {
            errors["Confidence"] = ["Confidence must be between 0 and 100"];
        }

        return errors;
    }
}
