using MongoDB.Driver;
using A360.Media.Api.Services;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Repository.Settings;
using A360.Security;

namespace A360.Media.Api.IoC;

public static class MediaServiceCollectionExtensions
{
    public static IServiceCollection AddMediaApiServices(
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

        services.AddScoped<EventLogRepository>();
        services.AddScoped<IEventLogRepository>(serviceProvider => serviceProvider.GetRequiredService<EventLogRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<EventLogRepository>());
        services.AddA360JwtAuthentication(configuration);
        // Sign-in + role permissions also when the frontend calls this service directly on its own port
        services.AddA360AccessGuard(configuration, null);
        services.AddScoped<IEventLogger>(serviceProvider => new EventLogger(
            serviceProvider.GetRequiredService<IEventLogRepository>(),
            "Media",
            serviceProvider.GetRequiredService<ICurrentUserAccessor>(),
            serviceProvider.GetService<IHttpContextAccessor>()));

        services.AddHostedService<MongoIndexHostedService>();

        services.AddSingleton<IMediaFileStorageService, MediaFileStorageService>();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }
}
