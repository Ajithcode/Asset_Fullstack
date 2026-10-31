using A360.EventLog.Api.Endpoints;

namespace A360.EventLog.Api.Middlewares;

public static class EventLogApplicationBuilderExtensions
{
    public static WebApplication UseEventLogApiMiddlewares(this WebApplication app)
    {
        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "EventLog" }))
            .WithName("EventLogHealth")
            .WithTags("Health");

        app.MapEventLogEndpoints();

        return app;
    }
}
