using A360.Asset.Api.Endpoints;
using A360.Security;

namespace A360.Asset.Api.Middlewares;

public static class AssetApplicationBuilderExtensions
{
    public static WebApplication UseAssetApiMiddlewares(this WebApplication app)
    {
        // Reads the bearer token forwarded by the gateway so event logs know who made each change.
        app.UseAuthentication();
        app.UseA360AccessGuard();

        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Asset" }))
            .WithName("AssetHealth")
            .WithTags("Health");

        app.MapAssetRegistrationEndpoints();
        app.MapAssetCategoryEndpoints();
        app.MapAssetCategoryTypeEndpoints();
        app.MapAssetModelEndpoints();
        app.MapAssetHierarchyEndpoints();
        app.MapAssetComponentEndpoints();
        app.MapAssetCustomFieldEndpoints();
        app.MapAssetFieldGroupEndpoints();
        app.MapAssetFieldLayoutEndpoints();
        app.MapAssetDocumentEndpoints();
        app.MapAssetImageEndpoints();
        app.MapAssetRelationshipEndpoints();
        app.MapAssetConditionAssessmentEndpoints();
        app.MapAssetCriticalityEndpoints();
        app.MapAssetRiskMitigationActionEndpoints();
        app.MapAssetImportEndpoints();
        app.MapAssetAssignmentEndpoints();
        app.MapAssetBulkAssignmentEndpoints();
        app.MapAssetAuditEndpoints();
        app.MapAssetDisposalEndpoints();
        app.MapAssetServiceRequestEndpoints();
        app.MapAssetDashboardEndpoints();
        app.MapAssetDedupEndpoints();
        app.MapAssetLifecycleEndpoints();

        return app;
    }
}
