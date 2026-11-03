using A360.Administration.Api.Endpoints;

namespace A360.Administration.Api.Middlewares;

public static class AdministrationApplicationBuilderExtensions
{
    public static WebApplication UseAdministrationApiMiddlewares(this WebApplication app)
    {
        // Reads the bearer token forwarded by the gateway so event logs know who made each change.
        app.UseAuthentication();

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

        return app;
    }
}
