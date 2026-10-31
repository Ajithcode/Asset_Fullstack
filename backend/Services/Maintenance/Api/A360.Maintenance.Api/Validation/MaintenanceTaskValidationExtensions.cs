
using A360.Maintenance.Api.Contracts;

namespace A360.Maintenance.Api.Validation;

public static class MaintenanceTaskValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateMaintenanceTaskRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.AssetName))
        {
            errors["AssetName"] =
                ["AssetName is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateMaintenanceTaskRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.AssetName))
        {
            errors["AssetName"] =
                ["AssetName is required"];
        }

        return errors;
    }
}
