using MongoDB.Driver;
using A360.Asset.Repository.Repositories;
using A360.MasterManagement.Repository.Repositories;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Repository.Settings;
using A360.Security;

namespace A360.MasterManagement.Api.IoC;

public static class MasterManagementServiceCollectionExtensions
{
    public static IServiceCollection AddMasterManagementApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var mongoDbSettings = new MongoDbSettings
        {
            ConnectionString = configuration[$"{MongoDbSettings.SectionName}:ConnectionString"] ?? string.Empty,
            DatabaseName = configuration[$"{MongoDbSettings.SectionName}:DatabaseName"] ?? string.Empty
        };

        mongoDbSettings.Validate();

        services.AddSingleton(mongoDbSettings);
        services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoDbSettings.ConnectionString));
        services.AddSingleton(serviceProvider =>
        {
            var client = serviceProvider.GetRequiredService<IMongoClient>();
            return client.GetDatabase(mongoDbSettings.DatabaseName);
        });

        services.AddSingleton<ISequenceGenerator, MongoSequenceGenerator>();

        services.AddScoped<EventLogRepository>();
        services.AddScoped<IEventLogRepository>(serviceProvider => serviceProvider.GetRequiredService<EventLogRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<EventLogRepository>());
        services.AddA360JwtAuthentication(configuration);
        services.AddScoped<IEventLogger>(serviceProvider => new EventLogger(
            serviceProvider.GetRequiredService<IEventLogRepository>(),
            "MasterManagement",
            serviceProvider.GetRequiredService<ICurrentUserAccessor>()));

        services.AddScoped<MasterMaintenanceRepository>();
        services.AddScoped<IMasterMaintenanceRepository>(serviceProvider => serviceProvider.GetRequiredService<MasterMaintenanceRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<MasterMaintenanceRepository>());

        services.AddScoped<CategoryRepository>();
        services.AddScoped<ICategoryRepository>(serviceProvider => serviceProvider.GetRequiredService<CategoryRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<CategoryRepository>());

        services.AddScoped<AssetTypeRepository>();
        services.AddScoped<IAssetTypeRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetTypeRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetTypeRepository>());

        services.AddScoped<AssignedCustodianRepository>();
        services.AddScoped<IAssignedCustodianRepository>(serviceProvider => serviceProvider.GetRequiredService<AssignedCustodianRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssignedCustodianRepository>());

        services.AddScoped<CurrentLocationRepository>();
        services.AddScoped<ICurrentLocationRepository>(serviceProvider => serviceProvider.GetRequiredService<CurrentLocationRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<CurrentLocationRepository>());

        services.AddScoped<AuditorDetailRepository>();
        services.AddScoped<IAuditorDetailRepository>(serviceProvider => serviceProvider.GetRequiredService<AuditorDetailRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AuditorDetailRepository>());

        services.AddScoped<PhysicalVerificationResultRepository>();
        services.AddScoped<IPhysicalVerificationResultRepository>(serviceProvider => serviceProvider.GetRequiredService<PhysicalVerificationResultRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<PhysicalVerificationResultRepository>());

        services.AddScoped<AssetTypeFieldRepository>();
        services.AddScoped<IAssetTypeFieldRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetTypeFieldRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetTypeFieldRepository>());

        services.AddScoped<ApiSyncStatusMasterRepository>();
        services.AddScoped<IApiSyncStatusMasterRepository>(serviceProvider => serviceProvider.GetRequiredService<ApiSyncStatusMasterRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<ApiSyncStatusMasterRepository>());

        services.AddScoped<StatusChangeRepository>();
        services.AddScoped<IStatusChangeRepository>(serviceProvider => serviceProvider.GetRequiredService<StatusChangeRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<StatusChangeRepository>());

        services.AddScoped<TagRepository>();
        services.AddScoped<ITagRepository>(serviceProvider => serviceProvider.GetRequiredService<TagRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<TagRepository>());

        services.AddScoped<DepreciationMethodRepository>();
        services.AddScoped<IDepreciationMethodRepository>(serviceProvider => serviceProvider.GetRequiredService<DepreciationMethodRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<DepreciationMethodRepository>());

        services.AddScoped<CostCenterRepository>();
        services.AddScoped<ICostCenterRepository>(serviceProvider => serviceProvider.GetRequiredService<CostCenterRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<CostCenterRepository>());

        services.AddScoped<AlertTypeRepository>();
        services.AddScoped<IAlertTypeRepository>(serviceProvider => serviceProvider.GetRequiredService<AlertTypeRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AlertTypeRepository>());

        services.AddScoped<ResolutionStatusRepository>();
        services.AddScoped<IResolutionStatusRepository>(serviceProvider => serviceProvider.GetRequiredService<ResolutionStatusRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<ResolutionStatusRepository>());

        services.AddScoped<CertificationTypeMasterRepository>();
        services.AddScoped<ICertificationTypeMasterRepository>(serviceProvider => serviceProvider.GetRequiredService<CertificationTypeMasterRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<CertificationTypeMasterRepository>());

        services.AddScoped<WorkTypeRepository>();
        services.AddScoped<IWorkTypeRepository>(serviceProvider => serviceProvider.GetRequiredService<WorkTypeRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<WorkTypeRepository>());

        services.AddScoped<PriorityRepository>();
        services.AddScoped<IPriorityRepository>(serviceProvider => serviceProvider.GetRequiredService<PriorityRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<PriorityRepository>());

        services.AddScoped<StatusMasterRepository>();
        services.AddScoped<IStatusMasterRepository>(serviceProvider => serviceProvider.GetRequiredService<StatusMasterRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<StatusMasterRepository>());

        services.AddScoped<ResourceTypeRepository>();
        services.AddScoped<IResourceTypeRepository>(serviceProvider => serviceProvider.GetRequiredService<ResourceTypeRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<ResourceTypeRepository>());

        services.AddScoped<SkillMasterRepository>();
        services.AddScoped<ISkillMasterRepository>(serviceProvider => serviceProvider.GetRequiredService<SkillMasterRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<SkillMasterRepository>());

        services.AddScoped<ShiftMasterRepository>();
        services.AddScoped<IShiftMasterRepository>(serviceProvider => serviceProvider.GetRequiredService<ShiftMasterRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<ShiftMasterRepository>());

        services.AddScoped<ChecklistTypeMasterRepository>();
        services.AddScoped<IChecklistTypeMasterRepository>(serviceProvider => serviceProvider.GetRequiredService<ChecklistTypeMasterRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<ChecklistTypeMasterRepository>());

        services.AddScoped<ResponseTypeMasterRepository>();
        services.AddScoped<IResponseTypeMasterRepository>(serviceProvider => serviceProvider.GetRequiredService<ResponseTypeMasterRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<ResponseTypeMasterRepository>());

        services.AddScoped<ConditionMasterRepository>();
        services.AddScoped<IConditionMasterRepository>(serviceProvider => serviceProvider.GetRequiredService<ConditionMasterRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<ConditionMasterRepository>());

        services.AddScoped<IssueTypeMasterRepository>();
        services.AddScoped<IIssueTypeMasterRepository>(serviceProvider => serviceProvider.GetRequiredService<IssueTypeMasterRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<IssueTypeMasterRepository>());

        services.AddScoped<SeverityMasterRepository>();
        services.AddScoped<ISeverityMasterRepository>(serviceProvider => serviceProvider.GetRequiredService<SeverityMasterRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<SeverityMasterRepository>());

        services.AddScoped<UnitMasterRepository>();
        services.AddScoped<IUnitMasterRepository>(serviceProvider => serviceProvider.GetRequiredService<UnitMasterRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<UnitMasterRepository>());

        services.AddScoped<PermitTypeMasterRepository>();
        services.AddScoped<IPermitTypeMasterRepository>(serviceProvider => serviceProvider.GetRequiredService<PermitTypeMasterRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<PermitTypeMasterRepository>());

        services.AddScoped<UpdateSourceMasterRepository>();
        services.AddScoped<IUpdateSourceMasterRepository>(serviceProvider => serviceProvider.GetRequiredService<UpdateSourceMasterRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<UpdateSourceMasterRepository>());

        services.AddScoped<ChartTypeMasterRepository>();
        services.AddScoped<IChartTypeMasterRepository>(serviceProvider => serviceProvider.GetRequiredService<ChartTypeMasterRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<ChartTypeMasterRepository>());

        services.AddScoped<PermissionMasterRepository>();
        services.AddScoped<IPermissionMasterRepository>(serviceProvider => serviceProvider.GetRequiredService<PermissionMasterRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<PermissionMasterRepository>());

        services.AddScoped<ModuleAccessMasterRepository>();
        services.AddScoped<IModuleAccessMasterRepository>(serviceProvider => serviceProvider.GetRequiredService<ModuleAccessMasterRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<ModuleAccessMasterRepository>());

        services.AddScoped<AssetRepository>();
        services.AddScoped<IAssetRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetRepository>());

        services.AddScoped<OrganizationRepository>();
        services.AddScoped<IOrganizationRepository>(serviceProvider => serviceProvider.GetRequiredService<OrganizationRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<OrganizationRepository>());

        services.AddScoped<BusinessUnitRepository>();
        services.AddScoped<IBusinessUnitRepository>(serviceProvider => serviceProvider.GetRequiredService<BusinessUnitRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<BusinessUnitRepository>());

        services.AddScoped<DepartmentRepository>();
        services.AddScoped<IDepartmentRepository>(serviceProvider => serviceProvider.GetRequiredService<DepartmentRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<DepartmentRepository>());

        services.AddScoped<SiteRepository>();
        services.AddScoped<ISiteRepository>(serviceProvider => serviceProvider.GetRequiredService<SiteRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<SiteRepository>());

        services.AddScoped<ManufacturerRepository>();
        services.AddScoped<IManufacturerRepository>(serviceProvider => serviceProvider.GetRequiredService<ManufacturerRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<ManufacturerRepository>());

        services.AddScoped<SupplierRepository>();
        services.AddScoped<ISupplierRepository>(serviceProvider => serviceProvider.GetRequiredService<SupplierRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<SupplierRepository>());

        services.AddHostedService<MongoIndexHostedService>();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }
}
