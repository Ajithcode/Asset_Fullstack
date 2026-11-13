using A360.Repository.Activity;

namespace A360.Wip.Api.Middlewares;

using A360.Wip.Api.Endpoints;
using A360.Security;

public static class WipApplicationBuilderExtensions
{
    public static WebApplication UseWipApiMiddlewares(
        this WebApplication app)
    {
        // Reads the bearer token forwarded by the gateway so event logs know who made each change.
        app.UseAuthentication();
        app.UseA360AccessGuard();
        app.UseA360WriteAudit();

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
                service = "Wip"
            }))
            .WithName("WipHealth")
            .WithTags("Health");

        app.MapJobMasterEndpoints();
        app.MapStatusMasterEndpoints();
        app.MapResourceMasterEndpoints();
        app.MapTaskMasterEndpoints();
        app.MapChecklistMasterEndpoints();
        app.MapChecklistItemEndpoints();
        app.MapLocationMasterEndpoints();
        app.MapAssetLinkingEndpoints();
        app.MapSlaMasterEndpoints();
        app.MapIssueMasterEndpoints();
        app.MapMaterialConsumptionEndpoints();
        app.MapPermitMasterEndpoints();
        app.MapProgressLogEndpoints();
        app.MapAlertMasterEndpoints();
        app.MapKpiMasterEndpoints();

        return app;
    }
}
