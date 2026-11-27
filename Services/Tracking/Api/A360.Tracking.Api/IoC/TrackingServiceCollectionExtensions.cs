using MongoDB.Driver;
using A360.Tracking.Api.Alerts;
using A360.Tracking.Api.Endpoints;
using A360.Tracking.Api.Locating;
using A360.Tracking.Repository.Repositories;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Repository.Settings;
using A360.Security;

namespace A360.Tracking.Api.IoC;

public static class TrackingServiceCollectionExtensions
{
    public static IServiceCollection AddTrackingApiServices(
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
        services.AddSingleton<A360.Tracking.Api.Locating.LocationPushHub>();
        services.AddHostedService(sp => sp.GetRequiredService<A360.Tracking.Api.Locating.LocationPushHub>());

        services.AddScoped<EventLogRepository>();
        services.AddScoped<IEventLogRepository>(serviceProvider => serviceProvider.GetRequiredService<EventLogRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<EventLogRepository>());
        services.AddA360JwtAuthentication(configuration);
        // Sign-in + role permissions also when the frontend calls this service directly on its own port
        services.AddA360AccessGuard(configuration, "Tracking & IoT", PushEndpoints.ActionsPath);
        services.AddScoped<IEventLogger>(serviceProvider => new EventLogger(
            serviceProvider.GetRequiredService<IEventLogRepository>(),
            "Tracking",
            serviceProvider.GetRequiredService<ICurrentUserAccessor>(),
            serviceProvider.GetService<IHttpContextAccessor>()));

        services.AddScoped<TrackingTagRepository>();
        services.AddScoped<ITrackingTagRepository>(serviceProvider => serviceProvider.GetRequiredService<TrackingTagRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<TrackingTagRepository>());

        services.AddScoped<TagEventRepository>();
        services.AddScoped<ITagEventRepository>(serviceProvider => serviceProvider.GetRequiredService<TagEventRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<TagEventRepository>());
        services.AddScoped<ITagAssetLookup, TagAssetLookup>();
        services.AddScoped<INetworkReadingRepository, NetworkReadingRepository>();

        services.AddScoped<FirmwareVersionRepository>();
        services.AddScoped<IFirmwareVersionRepository>(serviceProvider => serviceProvider.GetRequiredService<FirmwareVersionRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<FirmwareVersionRepository>());

        services.AddScoped<GeofenceRepository>();
        services.AddScoped<IGeofenceRepository>(serviceProvider => serviceProvider.GetRequiredService<GeofenceRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<GeofenceRepository>());

        services.AddScoped<AlertRuleRepository>();
        services.AddScoped<IAlertRuleRepository>(serviceProvider => serviceProvider.GetRequiredService<AlertRuleRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AlertRuleRepository>());

        services.AddScoped<DeviceHealthRepository>();
        services.AddScoped<IDeviceHealthRepository>(serviceProvider => serviceProvider.GetRequiredService<DeviceHealthRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<DeviceHealthRepository>());

        services.AddScoped<DeviceMaintenanceRepository>();
        services.AddScoped<IDeviceMaintenanceRepository>(serviceProvider => serviceProvider.GetRequiredService<DeviceMaintenanceRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<DeviceMaintenanceRepository>());

        services.AddScoped<DeviceModelRepository>();
        services.AddScoped<IDeviceModelRepository>(serviceProvider => serviceProvider.GetRequiredService<DeviceModelRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<DeviceModelRepository>());

        services.AddScoped<DeviceTypeRepository>();
        services.AddScoped<IDeviceTypeRepository>(serviceProvider => serviceProvider.GetRequiredService<DeviceTypeRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<DeviceTypeRepository>());

        services.AddScoped<ReplacementDeviceRepository>();
        services.AddScoped<IReplacementDeviceRepository>(serviceProvider => serviceProvider.GetRequiredService<ReplacementDeviceRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<ReplacementDeviceRepository>());

        services.AddScoped<SensorRepository>();
        services.AddScoped<ISensorRepository>(serviceProvider => serviceProvider.GetRequiredService<SensorRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<SensorRepository>());

        services.AddScoped<IoTDeviceRepository>();
        services.AddScoped<IIoTDeviceRepository>(serviceProvider => serviceProvider.GetRequiredService<IoTDeviceRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<IoTDeviceRepository>());

        services.AddScoped<LocationEventRepository>();
        services.AddScoped<ILocationEventRepository>(serviceProvider => serviceProvider.GetRequiredService<LocationEventRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<LocationEventRepository>());

        services.AddScoped<DeviceConfigurationRepository>();
        services.AddScoped<IDeviceConfigurationRepository>(serviceProvider => serviceProvider.GetRequiredService<DeviceConfigurationRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<DeviceConfigurationRepository>());

        services.AddScoped<IoTAlertRepository>();
        services.AddScoped<IIoTAlertRepository>(serviceProvider => serviceProvider.GetRequiredService<IoTAlertRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<IoTAlertRepository>());

        services.AddScoped<AssetUtilizationRepository>();
        services.AddScoped<IAssetUtilizationRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetUtilizationRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetUtilizationRepository>());

        services.AddScoped<TelemetryReadingRepository>();
        services.AddScoped<ITelemetryReadingRepository>(serviceProvider => serviceProvider.GetRequiredService<TelemetryReadingRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<TelemetryReadingRepository>());

        services.AddScoped<DeviceConfigurationDependencies>();

        services.AddHostedService<MongoIndexHostedService>();

        // Floor plan zone alerts (enter / exit / dwell from live tag readings) and their desktop notifications
        // Tracking & Telemetry page data (live tag readings → assets, location, KPIs)
        services.AddSingleton<A360.Tracking.Api.Telemetry.TelemetryOverviewService>();
        services.AddSingleton<A360.Tracking.Api.Telemetry.RealTimeLocationService>();
        services.AddSingleton<A360.Tracking.Api.Telemetry.TagLocationService>();
        services.AddSingleton<A360.Tracking.Api.Telemetry.AssetMovementService>();
        services.AddSingleton<A360.Tracking.Api.Alerts.GeofenceAlertsService>();
        services.AddSingleton<A360.Tracking.Api.Endpoints.SensorParameterStore>();

        services.AddSingleton<MapZoneLookup>();
        services.AddSingleton<PushSender>();
        services.AddSingleton<AlertNotifications>();
        services.AddSingleton<AlertEmailNotifier>();
        services.AddHostedService<ZoneGeofenceEngine>();
        services.AddHostedService<AlertNotificationDispatcher>();
        services.AddLocating(configuration);
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }
}
