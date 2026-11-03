using A360.MasterDataManagement.Api.Endpoints;

namespace A360.MasterDataManagement.Api.Middlewares;

public static class MasterDataManagementApplicationBuilderExtensions
{
    public static WebApplication UseMasterDataManagementApiMiddlewares(this WebApplication app)
    {
        // Reads the bearer token forwarded by the gateway so event logs know who made each change.
        app.UseAuthentication();

        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "MasterDataManagement" }))
            .WithName("MasterDataManagementHealth")
            .WithTags("Health");

        app.MapMasterDataItemEndpoints();
        app.MapLocationEndpoints();
        app.MapBusinessUnitEndpoints();
        app.MapOrganizationEndpoints();
        app.MapDepartmentEndpoints();
        app.MapSiteEndpoints();
        app.MapBuildingEndpoints();
        app.MapFloorEndpoints();
        app.MapZoneEndpoints();
        app.MapAssetCategoryEndpoints();
        app.MapAssetTypeEndpoints();
        app.MapAssetModelEndpoints();
        app.MapManufacturerEndpoints();
        app.MapConditionEndpoints();
        app.MapCriticalityEndpoints();
        app.MapEmployeeEndpoints();
        app.MapTechnicianEndpoints();
        app.MapSkillEndpoints();
        app.MapTeamEndpoints();
        app.MapShiftEndpoints();
        app.MapSupplierEndpoints();
        app.MapServiceProviderEndpoints();
        app.MapAmcVendorEndpoints();
        app.MapMaintenanceTypeEndpoints();
        app.MapWorkTypeEndpoints();
        app.MapFailureCodeEndpoints();
        app.MapTaskTemplateEndpoints();
        app.MapChecklistEndpoints();
        app.MapPrioritySeverityEndpoints();
        app.MapItemCategoryEndpoints();
        app.MapUomEndpoints();
        app.MapItemEndpoints();
        app.MapWarehouseEndpoints();
        app.MapStoreEndpoints();
        app.MapBinEndpoints();
        app.MapStockStatusEndpoints();
        app.MapCertificationTypeEndpoints();
        app.MapRegulationEndpoints();
        app.MapStandardEndpoints();
        app.MapPermitTypeEndpoints();
        app.MapApiSyncStatusEndpoints();
        app.MapUpdateSourceEndpoints();
        app.MapChartTypeEndpoints();
        app.MapSystemSettingsEndpoints();
        app.MapModuleAccessEndpoints();
        app.MapPermissionEndpoints();
        app.MapRoleEndpoints();
        app.MapSystemUserEndpoints();

        return app;
    }
}
