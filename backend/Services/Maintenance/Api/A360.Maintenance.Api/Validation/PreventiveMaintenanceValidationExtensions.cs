
using A360.Maintenance.Api.Contracts;

namespace A360.Maintenance.Api.Validation;

public static class PreventiveMaintenanceValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreatePreventiveMaintenanceRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.PmScheduleId))
        {
            errors["PmScheduleId"] =
                ["PmScheduleId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Frequency))
        {
            errors["Frequency"] =
                ["Frequency is required"];
        }

        if (string.IsNullOrWhiteSpace(request.TriggerType))
        {
            errors["TriggerType"] =
                ["TriggerType is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdatePreventiveMaintenanceRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AssetId))
        {
            errors["AssetId"] =
                ["AssetId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.Frequency))
        {
            errors["Frequency"] =
                ["Frequency is required"];
        }

        if (string.IsNullOrWhiteSpace(request.TriggerType))
        {
            errors["TriggerType"] =
                ["TriggerType is required"];
        }

        return errors;
    }
}
