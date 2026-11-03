using A360.Tracking.Api.Endpoints;

namespace A360.Tracking.Api.Middlewares;

public static class TrackingApplicationBuilderExtensions
{
    public static WebApplication UseTrackingApiMiddlewares(this WebApplication app)
    {
        // Reads the bearer token forwarded by the gateway so event logs know who made each change.
        app.UseAuthentication();

        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Tracking" }))
            .WithName("TrackingHealth")
            .WithTags("Health");

        app.MapTrackingTagEndpoints();
        app.MapFirmwareVersionEndpoints();
        app.MapGeofenceEndpoints();
        app.MapAlertRuleEndpoints();
        app.MapDeviceHealthEndpoints();
        app.MapDeviceMaintenanceEndpoints();
        app.MapDeviceModelEndpoints();
        app.MapReplacementDeviceEndpoints();
        app.MapSensorEndpoints();
        app.MapIoTDeviceEndpoints();
        app.MapLocationEventEndpoints();
        app.MapZoneDrawingEndpoints();
        app.MapDeviceDrawingEndpoints();

        return app;
    }
}
