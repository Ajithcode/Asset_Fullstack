using A360.Inspection.Api.Endpoints;

namespace A360.Inspection.Api.Middlewares;

public static class InspectionApplicationBuilderExtensions
{
    public static WebApplication UseInspectionApiMiddlewares(this WebApplication app)
    {
        // Reads the bearer token forwarded by the gateway so event logs know who made each change.
        app.UseAuthentication();

        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Inspection" }))
            .WithName("InspectionHealth")
            .WithTags("Health");

        app.MapInspectionTypeEndpoints();
        app.MapTaskCategoryEndpoints();
        app.MapInspectionTaskEndpoints();
        app.MapFailureReasonEndpoints();
        app.MapDefectEndpoints();
        app.MapSeverityEndpoints();
        app.MapPriorityEndpoints();
        app.MapSignatureAndStampEndpoints();
        app.MapNotificationTemplateEndpoints();
        app.MapReportTemplateEndpoints();
        app.MapNumberingSequenceEndpoints();
        app.MapHolidayAndWorkingCalendarEndpoints();
        app.MapChecklistEndpoints();

        return app;
    }
}
