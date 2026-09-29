using A360.Locating.Api.Endpoints;

namespace A360.Locating.Api.Middlewares;

public static class LocatingApplicationBuilderExtensions
{
    public static WebApplication UseLocatingApiMiddlewares(this WebApplication app)
    {
        // Reads the bearer token forwarded by the gateway so event logs know who made each change.
        app.UseAuthentication();

        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Locating" }))
            .WithName("LocatingHealth")
            .WithTags("Health");

        app.MapZoneDrawingEndpoints();
        app.MapDeviceDrawingEndpoints();

        return app;
    }
}
