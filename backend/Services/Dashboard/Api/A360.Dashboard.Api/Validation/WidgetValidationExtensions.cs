using A360.Dashboard.Api.Contracts;

namespace A360.Dashboard.Api.Validation;

public static class WidgetValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateWidgetRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireDashboardId(errors, request.DashboardId);
        RequireModuleName(errors, request.ModuleName);

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateWidgetRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        RequireDashboardId(errors, request.DashboardId);
        RequireModuleName(errors, request.ModuleName);

        return errors;
    }

    private static void RequireDashboardId(Dictionary<string, string[]> errors, string? dashboardId)
    {
        if (string.IsNullOrWhiteSpace(dashboardId))
        {
            errors["DashboardId"] = ["DashboardId is required"];
        }
    }

    private static void RequireModuleName(Dictionary<string, string[]> errors, string? moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            errors["ModuleName"] = ["ModuleName is required"];
        }
    }
}
