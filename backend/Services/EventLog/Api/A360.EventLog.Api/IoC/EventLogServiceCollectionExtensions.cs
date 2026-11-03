using MongoDB.Driver;
using A360.Repository.Repositories;
using A360.Repository.Settings;

namespace A360.EventLog.Api.IoC;

public static class EventLogServiceCollectionExtensions
{
    public static IServiceCollection AddEventLogApiServices(
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

        services.AddHostedService<MongoIndexHostedService>();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }
}
