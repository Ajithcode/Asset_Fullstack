
namespace A360.Maintenance.Api.Middlewares;

using A360.Maintenance.Api.Endpoints;

public static class MaintenanceApplicationBuilderExtensions
{
    public static WebApplication UseMaintenanceApiMiddlewares(
        this WebApplication app)
    {
        // Reads the bearer token forwarded by the gateway so event logs know who made each change.
        app.UseAuthentication();

        if (app.Environment.IsDevelopment() ||
            app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();

            app.UseSwaggerUI();
        }

        app.MapGet("/health", () =>
            Results.Ok(new
            {
                status = "Healthy",
                service = "Maintenance"
            }))
            .WithName("MaintenanceHealth")
            .WithTags("Health");

        app.MapWorkOrderEndpoints();
        app.MapMaintenanceTaskEndpoints();
        app.MapPreventiveMaintenanceEndpoints();

        app.MapPredictiveMaintenanceEndpoints();
        app.MapIssueReportEndpoints();
        app.MapSparePartEndpoints();
        app.MapTechnicianEndpoints();
        app.MapVendorAmcEndpoints();
        app.MapCostTrackingEndpoints();
        app.MapDowntimeRecordEndpoints();
        app.MapPerformanceMetricEndpoints();
        app.MapComplianceInspectionEndpoints();

        return app;
    }
}
