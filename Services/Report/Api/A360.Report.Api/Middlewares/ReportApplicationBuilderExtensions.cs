using A360.Report.Api.Endpoints;

namespace A360.Report.Api.Middlewares;

public static class ReportApplicationBuilderExtensions
{
    public static WebApplication UseReportApiMiddlewares(this WebApplication app)
    {
        // Reads the bearer token forwarded by the gateway so event logs know who made each change.
        app.UseAuthentication();

        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Report" }))
            .WithName("ReportHealth")
            .WithTags("Health");

        app.MapReportEndpoints();

        return app;
    }
}
