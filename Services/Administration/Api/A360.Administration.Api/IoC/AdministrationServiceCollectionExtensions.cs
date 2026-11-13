using MongoDB.Driver;
using A360.Administration.Api.Accounts;
using A360.Administration.Api.Security;
using A360.Administration.Repository.Repositories;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Repository.Settings;
using A360.Security;

namespace A360.Administration.Api.IoC;

public static class AdministrationServiceCollectionExtensions
{
    public static IServiceCollection AddAdministrationApiServices(
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

        services.AddA360JwtAuthentication(configuration);
        // Sign-in + role permissions also when the frontend calls this service directly on its own port
        services.AddA360AccessGuard(configuration, "Administration");

        services.AddScoped<EventLogRepository>();
        services.AddScoped<IEventLogRepository>(serviceProvider => serviceProvider.GetRequiredService<EventLogRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<EventLogRepository>());
        services.AddScoped<IEventLogger>(serviceProvider => new EventLogger(
            serviceProvider.GetRequiredService<IEventLogRepository>(),
            "Administration",
            serviceProvider.GetRequiredService<ICurrentUserAccessor>()));

        services.AddScoped<LocationRepository>();
        services.AddScoped<ILocationRepository>(serviceProvider => serviceProvider.GetRequiredService<LocationRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<LocationRepository>());

        services.AddScoped<UserRepository>();
        services.AddScoped<IUserRepository>(serviceProvider => serviceProvider.GetRequiredService<UserRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<UserRepository>());

        // Sign-in accounts for User Management users live in the UserAccount service (same host in the container)
        // The UserAccount service requires a signed-in user, so the admin's own token goes along
        services.AddHttpContextAccessor();
        services.AddTransient<ForwardAuthorizationHandler>();
        services.AddHttpClient<LoginAccountClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Services:UserAccountUrl"] ?? "http://localhost:5018/");
            client.Timeout = TimeSpan.FromSeconds(15);
        })
            .AddHttpMessageHandler<ForwardAuthorizationHandler>();

        services.AddScoped<RoleRepository>();
        services.AddScoped<IRoleRepository>(serviceProvider => serviceProvider.GetRequiredService<RoleRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<RoleRepository>());

        services.AddScoped<AccessPolicyRepository>();
        services.AddScoped<IAccessPolicyRepository>(serviceProvider => serviceProvider.GetRequiredService<AccessPolicyRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AccessPolicyRepository>());

        services.AddScoped<NotificationTemplateRepository>();
        services.AddScoped<INotificationTemplateRepository>(serviceProvider => serviceProvider.GetRequiredService<NotificationTemplateRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<NotificationTemplateRepository>());

        services.AddScoped<NumberingSequenceRepository>();
        services.AddScoped<INumberingSequenceRepository>(serviceProvider => serviceProvider.GetRequiredService<NumberingSequenceRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<NumberingSequenceRepository>());

        services.AddScoped<ChecklistItemRepository>();
        services.AddScoped<IChecklistItemRepository>(serviceProvider => serviceProvider.GetRequiredService<ChecklistItemRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<ChecklistItemRepository>());

        services.AddScoped<SystemConfigurationRepository>();
        services.AddScoped<ISystemConfigurationRepository>(serviceProvider => serviceProvider.GetRequiredService<SystemConfigurationRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<SystemConfigurationRepository>());

        services.AddScoped<IntegrationRepository>();
        services.AddScoped<IIntegrationRepository>(serviceProvider => serviceProvider.GetRequiredService<IntegrationRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<IntegrationRepository>());

        services.AddScoped<DataImportJobRepository>();
        services.AddScoped<IDataImportJobRepository>(serviceProvider => serviceProvider.GetRequiredService<DataImportJobRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<DataImportJobRepository>());

        services.AddScoped<DeviceAssetPlotRepository>();
        services.AddScoped<IDeviceAssetPlotRepository>(serviceProvider => serviceProvider.GetRequiredService<DeviceAssetPlotRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<DeviceAssetPlotRepository>());

        services.AddScoped<MapLibraryZoneRepository>();
        services.AddScoped<IMapLibraryZoneRepository>(serviceProvider => serviceProvider.GetRequiredService<MapLibraryZoneRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<MapLibraryZoneRepository>());

        services.AddScoped<MapGeneralSettingsRepository>();
        services.AddScoped<IMapGeneralSettingsRepository>(serviceProvider => serviceProvider.GetRequiredService<MapGeneralSettingsRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<MapGeneralSettingsRepository>());

        services.AddScoped<ZoneGeofenceRepository>();
        services.AddScoped<IZoneGeofenceRepository>(serviceProvider => serviceProvider.GetRequiredService<ZoneGeofenceRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<ZoneGeofenceRepository>());

        services.AddScoped<ZoneDrawingRepository>();
        services.AddScoped<IZoneDrawingRepository>(serviceProvider => serviceProvider.GetRequiredService<ZoneDrawingRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<ZoneDrawingRepository>());

        services.AddScoped<DeviceDrawingRepository>();
        services.AddScoped<IDeviceDrawingRepository>(serviceProvider => serviceProvider.GetRequiredService<DeviceDrawingRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<DeviceDrawingRepository>());

        // Maps & Floor Plans
        services.AddScoped<FloorPlanRepository>();
        services.AddScoped<IFloorPlanRepository>(serviceProvider => serviceProvider.GetRequiredService<FloorPlanRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<FloorPlanRepository>());

        services.AddScoped<MapZoneRepository>();
        services.AddScoped<IMapZoneRepository>(serviceProvider => serviceProvider.GetRequiredService<MapZoneRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<MapZoneRepository>());

        // Location (site > building > floor > zone) and device ids of drawn zones / plots
        services.AddScoped<IMapLocationResolver, MapLocationResolver>();

        services.AddScoped<MapPlotRepository>();
        services.AddScoped<IMapPlotRepository>(serviceProvider => serviceProvider.GetRequiredService<MapPlotRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<MapPlotRepository>());

        services.AddScoped<MapLayerRepository>();
        services.AddScoped<IMapLayerRepository>(serviceProvider => serviceProvider.GetRequiredService<MapLayerRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<MapLayerRepository>());

        services.AddScoped<MapLayerSetRepository>();
        services.AddScoped<IMapLayerSetRepository>(serviceProvider => serviceProvider.GetRequiredService<MapLayerSetRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<MapLayerSetRepository>());

        services.AddScoped<MapSettingsRepository>();
        services.AddScoped<IMapSettingsRepository>(serviceProvider => serviceProvider.GetRequiredService<MapSettingsRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<MapSettingsRepository>());

        // Built eagerly so a missing or malformed encryption key stops the service at startup.
        services.AddSingleton<ISecretProtector>(new AesGcmSecretProtector(configuration));

        services.AddHostedService<MongoIndexHostedService>();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "A360 Administration API",
                Version = "v1"
            });
        });

        return services;
    }
}
