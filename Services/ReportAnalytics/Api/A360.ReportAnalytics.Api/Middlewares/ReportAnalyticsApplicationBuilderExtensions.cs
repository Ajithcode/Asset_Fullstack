using A360.Repository.Activity;
using A360.ReportAnalytics.Api.Endpoints;
using A360.Security;

namespace A360.ReportAnalytics.Api.Middlewares;

public static class ReportAnalyticsApplicationBuilderExtensions
{
    public static WebApplication UseReportAnalyticsApiMiddlewares(this WebApplication app)
    {
        // Reads the bearer token forwarded by the gateway so event logs know who made each change.
        app.UseAuthentication();
        app.UseA360AccessGuard();
        app.UseA360WriteAudit();

        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "ReportAnalytics" }))
            .WithName("ReportAnalyticsHealth")
            .WithTags("Health");

        app.MapStandardReportEndpoints();
        app.MapCustomReportEndpoints();
        app.MapCustomReportBuilderEndpoints();
        app.MapInteractiveDashboardEndpoints();
        app.MapInteractiveDashboardDataEndpoints();
        app.MapAssetPerformanceEndpoints();
        app.MapMaintenanceAnalyticsEndpoints();
        app.MapInspectionAnalyticsEndpoints();
        app.MapProductionAnalyticsEndpoints();

        return app;
    }
}
