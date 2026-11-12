using A360.Repository.Activity;
using A360.Administration.Api.Endpoints;
using A360.Security;

namespace A360.Administration.Api.Middlewares;

public static class AdministrationApplicationBuilderExtensions
{
    public static WebApplication UseAdministrationApiMiddlewares(this WebApplication app)
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

        app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Administration" }))
            .WithName("AdministrationHealth")
            .WithTags("Health");

        app.MapLocationEndpoints();
        app.MapUserEndpoints();
        app.MapRoleEndpoints();
        app.MapAccessPolicyEndpoints();
        app.MapNotificationTemplateEndpoints();
        app.MapNumberingSequenceEndpoints();
        app.MapChecklistItemEndpoints();
        app.MapSystemConfigurationEndpoints();
        app.MapIntegrationEndpoints();
        app.MapDataImportJobEndpoints();
        app.MapDeviceAssetPlotEndpoints();
        app.MapMapLibraryZoneEndpoints();
        app.MapMapGeneralSettingsEndpoints();
        app.MapZoneGeofenceEndpoints();
        app.MapZoneDrawingEndpoints();
        app.MapDeviceDrawingEndpoints();
        app.MapMapEndpoints();

        return app;
    }
}
