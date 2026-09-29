using A360.MasterManagement.Api.Endpoints;

namespace A360.MasterManagement.Api.Middlewares;

public static class MasterManagementApplicationBuilderExtensions
{
    public static WebApplication UseMasterManagementApiMiddlewares(this WebApplication app)
    {
        // Reads the bearer token forwarded by the gateway so event logs know who made each change.
        app.UseAuthentication();

        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "MasterManagement" }))
            .WithName("MasterManagementHealth")
            .WithTags("Health");

        app.MapMasterMaintenanceEndpoints();
        app.MapCategoryEndpoints();
        app.MapAssetTypeEndpoints();
        app.MapAssignedCustodianEndpoints();
        app.MapCurrentLocationEndpoints();
        app.MapAuditorDetailEndpoints();
        app.MapPhysicalVerificationResultEndpoints();
        app.MapAssetTypeFieldEndpoints();
        app.MapApiSyncStatusMasterEndpoints();
        app.MapStatusChangeEndpoints();
        app.MapTagEndpoints();
        app.MapDepreciationMethodEndpoints();
        app.MapCostCenterEndpoints();
        app.MapAlertTypeEndpoints();
        app.MapResolutionStatusEndpoints();
        app.MapCertificationTypeMasterEndpoints();
        app.MapWorkTypeEndpoints();
        app.MapPriorityEndpoints();
        app.MapStatusMasterEndpoints();
        app.MapResourceTypeEndpoints();
        app.MapSkillMasterEndpoints();
        app.MapShiftMasterEndpoints();
        app.MapChecklistTypeMasterEndpoints();
        app.MapResponseTypeMasterEndpoints();
        app.MapConditionMasterEndpoints();
        app.MapIssueTypeMasterEndpoints();
        app.MapSeverityMasterEndpoints();
        app.MapUnitMasterEndpoints();
        app.MapPermitTypeMasterEndpoints();
        app.MapUpdateSourceMasterEndpoints();
        app.MapChartTypeMasterEndpoints();
        app.MapPermissionMasterEndpoints();
        app.MapModuleAccessMasterEndpoints();
        app.MapOrganizationEndpoints();
        app.MapBusinessUnitEndpoints();
        app.MapDepartmentEndpoints();
        app.MapSiteEndpoints();
        app.MapManufacturerEndpoints();
        app.MapSupplierEndpoints();

        return app;
    }
}
