using A360.Dashboard.Api.Contracts;

namespace A360.Dashboard.Api.Validation;

public static class DashboardValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateDashboardRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireDashboardName(errors, request.DashboardName);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateDashboardRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireDashboardName(errors, request.DashboardName);

        return errors;
    }

    private static void RequireDashboardName(Dictionary<string, string[]> errors, string? dashboardName)
    {
        if (string.IsNullOrWhiteSpace(dashboardName))
        {
            errors["DashboardName"] = ["DashboardName is required"];
        }
    }
}
