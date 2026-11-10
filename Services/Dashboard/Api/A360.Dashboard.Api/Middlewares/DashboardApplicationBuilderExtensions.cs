using A360.Dashboard.Api.Endpoints;
using A360.Security;

namespace A360.Dashboard.Api.Middlewares;

public static class DashboardApplicationBuilderExtensions
{
    public static WebApplication UseDashboardApiMiddlewares(this WebApplication app)
    {
        // Reads the bearer token forwarded by the gateway so event logs know who made each change.
        app.UseAuthentication();
        app.UseA360AccessGuard();

        // Needed for the live network status endpoint.
        app.UseWebSockets();

        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Dashboard" }))
            .WithName("DashboardHealth")
            .WithTags("Health");

        app.MapDashboardEndpoints();
        app.MapWidgetEndpoints();
        app.MapAddWidgetEndpoints();
        app.MapNetworkStatusEndpoints();
        app.MapDashboardAssetEndpoints();
        app.MapMobileDashboardEndpoints();

        return app;
    }
}
