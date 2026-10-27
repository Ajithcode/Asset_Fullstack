using MongoDB.Driver;
using A360.Locating.Repository.Repositories;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Repository.Settings;
using A360.Security;

namespace A360.Locating.Api.IoC;

public static class LocatingServiceCollectionExtensions
{
    public static IServiceCollection AddLocatingApiServices(
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

        services.AddScoped<EventLogRepository>();
        services.AddScoped<IEventLogRepository>(serviceProvider => serviceProvider.GetRequiredService<EventLogRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<EventLogRepository>());
        services.AddScoped<IEventLogger>(serviceProvider => new EventLogger(
            serviceProvider.GetRequiredService<IEventLogRepository>(),
            "Locating",
            serviceProvider.GetRequiredService<ICurrentUserAccessor>()));

        services.AddScoped<ZoneDrawingRepository>();
        services.AddScoped<IZoneDrawingRepository>(serviceProvider => serviceProvider.GetRequiredService<ZoneDrawingRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<ZoneDrawingRepository>());

        services.AddScoped<DeviceDrawingRepository>();
        services.AddScoped<IDeviceDrawingRepository>(serviceProvider => serviceProvider.GetRequiredService<DeviceDrawingRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<DeviceDrawingRepository>());

        services.AddScoped<ILocationRepository, LocationRepository>();

        services.AddHostedService<MongoIndexHostedService>();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "A360 Locating API",
                Version = "v1"
            });
        });

        return services;
    }
}
