using A360.Dashboard.Api.Endpoints;

namespace A360.Dashboard.Api.Middlewares;

public static class DashboardApplicationBuilderExtensions
{
    public static WebApplication UseDashboardApiMiddlewares(this WebApplication app)
    {
        // Reads the bearer token forwarded by the gateway so event logs know who made each change.
        app.UseAuthentication();

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

        return app;
    }
}
