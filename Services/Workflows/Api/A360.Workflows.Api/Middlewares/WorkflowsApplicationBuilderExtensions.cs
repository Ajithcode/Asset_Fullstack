
namespace A360.Workflows.Api.Middlewares;

using A360.Workflows.Api.Endpoints;

public static class WorkflowsApplicationBuilderExtensions
{
    public static WebApplication UseWorkflowsApiMiddlewares(
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
                service = "Workflows"
            }))
            .WithName("WorkflowsHealth")
            .WithTags("Health");

        app.MapWorkflowListEndpoints();

        app.MapWorkflowBuilderEndpoints();

        app.MapWorkflowInstanceEndpoints();

        app.MapWorkflowApprovalTaskEndpoints();

        app.MapWorkflowInsightEndpoints();

        return app;
    }
}
