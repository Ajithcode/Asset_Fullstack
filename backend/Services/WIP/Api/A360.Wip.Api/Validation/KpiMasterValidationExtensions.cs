
using A360.Wip.Api.Contracts;

namespace A360.Wip.Api.Validation;

public static class KpiMasterValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateKpiMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.KpiId))
        {
            errors["KpiId"] =
                ["KpiId is required"];
        }

        if (string.IsNullOrWhiteSpace(request.KpiName))
        {
            errors["KpiName"] =
                ["KpiName is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateKpiMasterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.KpiName))
        {
            errors["KpiName"] =
                ["KpiName is required"];
        }

        return errors;
    }
}
