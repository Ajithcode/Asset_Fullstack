
using A360.Wip.Api.Contracts;

namespace A360.Wip.Api.Validation;

public static class AlertMasterValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateAlertMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AlertId))
        {
            errors["AlertId"] =
                ["AlertId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.AlertType))
        {
            errors["AlertType"] =
                ["AlertType is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateAlertMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AlertType))
        {
            errors["AlertType"] =
                ["AlertType is required"];
        }

        return errors;
    }
}
