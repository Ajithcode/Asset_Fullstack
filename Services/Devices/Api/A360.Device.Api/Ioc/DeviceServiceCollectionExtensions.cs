
using MongoDB.Driver;
using A360.Devices.Repository.Repositories;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Repository.Settings;
using A360.Security;

namespace A360.Devices.Api.IoC;

public static class DeviceServiceCollectionExtensions
{
    public static IServiceCollection AddDeviceApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var mongoDbSettings = new MongoDbSettings
        {
            ConnectionString =
                configuration[$"{MongoDbSettings.SectionName}:ConnectionString"]
                ?? string.Empty,

            DatabaseName =
                configuration[$"{MongoDbSettings.SectionName}:DatabaseName"]
                ?? string.Empty
        };

        mongoDbSettings.Validate();

        services.AddSingleton(mongoDbSettings);

        services.AddSingleton<IMongoClient>(_ =>
            new MongoClient(mongoDbSettings.ConnectionString));

        services.AddSingleton(serviceProvider =>
        {
            var client =
                serviceProvider.GetRequiredService<IMongoClient>();

            return client.GetDatabase(
                mongoDbSettings.DatabaseName);
        });

        // Event Log Repository Registration

        services.AddScoped<EventLogRepository>();
        services.AddScoped<IEventLogRepository>(serviceProvider => serviceProvider.GetRequiredService<EventLogRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<EventLogRepository>());
        services.AddA360JwtAuthentication(configuration);
        services.AddScoped<IEventLogger>(serviceProvider => new EventLogger(
            serviceProvider.GetRequiredService<IEventLogRepository>(),
            "Device",
            serviceProvider.GetRequiredService<ICurrentUserAccessor>()));

        // Device Repository Registration

        services.AddScoped<DeviceRepository>();

        services.AddScoped<IDeviceRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<DeviceRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<DeviceRepository>());

        // Mongo Index Hosted Service

        services.AddHostedService<MongoIndexHostedService>();

        // Swagger

        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(
                "v1",
                new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "A360 Devices API",
                    Version = "v1"
                });
        });

        return services;
    }
}

