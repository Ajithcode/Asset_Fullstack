using MongoDB.Driver;
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

        services.AddScoped<EventLogRepository>();
        services.AddScoped<IEventLogRepository>(serviceProvider => serviceProvider.GetRequiredService<EventLogRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<EventLogRepository>());
        services.AddA360JwtAuthentication(configuration);
        services.AddScoped<IEventLogger>(serviceProvider => new EventLogger(
            serviceProvider.GetRequiredService<IEventLogRepository>(),
            "Tracking",
            serviceProvider.GetRequiredService<ICurrentUserAccessor>()));

        services.AddScoped<TrackingTagRepository>();
        services.AddScoped<ITrackingTagRepository>(serviceProvider => serviceProvider.GetRequiredService<TrackingTagRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<TrackingTagRepository>());

        services.AddScoped<FirmwareVersionRepository>();
        services.AddScoped<IFirmwareVersionRepository>(serviceProvider => serviceProvider.GetRequiredService<FirmwareVersionRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<FirmwareVersionRepository>());

        services.AddScoped<GeofenceRepository>();
        services.AddScoped<IGeofenceRepository>(serviceProvider => serviceProvider.GetRequiredService<GeofenceRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<GeofenceRepository>());

        services.AddScoped<DeviceHealthRepository>();
        services.AddScoped<IDeviceHealthRepository>(serviceProvider => serviceProvider.GetRequiredService<DeviceHealthRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<DeviceHealthRepository>());

        services.AddScoped<DeviceMaintenanceRepository>();
        services.AddScoped<IDeviceMaintenanceRepository>(serviceProvider => serviceProvider.GetRequiredService<DeviceMaintenanceRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<DeviceMaintenanceRepository>());

        services.AddScoped<DeviceModelRepository>();
        services.AddScoped<IDeviceModelRepository>(serviceProvider => serviceProvider.GetRequiredService<DeviceModelRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<DeviceModelRepository>());

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

        services.AddHostedService<MongoIndexHostedService>();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }
}
