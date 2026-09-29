using A360.Asset.Api.Endpoints;

namespace A360.Asset.Api.Middlewares;

public static class AssetApplicationBuilderExtensions
{
    public static WebApplication UseAssetApiMiddlewares(this WebApplication app)
    {
        // Reads the bearer token forwarded by the gateway so event logs know who made each change.
        app.UseAuthentication();

        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Asset" }))
            .WithName("AssetHealth")
            .WithTags("Health");

        app.MapAssetEndpoints();
        app.MapAssetLocationEndpoints();
        app.MapAssetOwnershipEndpoints();
        app.MapAssetLifecycleEndpoints();
        app.MapAssetTrackingAndTelemetryEndpoints();
        app.MapAssetMaintenanceAndServiceEndpoints();
        app.MapAssetUtilizationAndPerformanceEndpoints();
        app.MapAssetFinancialDetailsEndpoints();
        app.MapAssetDocumentsEndpoints();
        app.MapAssetContractEndpoints();
        app.MapAssetIncidentEndpoints();
        app.MapAssetAuditAndVerificationEndpoints();
        app.MapAssetActivityEndpoints();
        app.MapAssetDomainEndpoints();
        app.MapAssetIntegrationEndpoints();
        app.MapAssetCertificationEndpoints();
        app.MapAssetAuditEndpoints();
        app.MapAssetMovementEndpoints();
        app.MapAssetDisposalEndpoints();
        app.MapAssetCheckoutEndpoints();
        app.MapAssetCheckinEndpoints();
        app.MapTaggedAssetsEndpoints();

        return app;
    }
}
