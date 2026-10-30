using MongoDB.Driver;
using A360.Dashboard.Repository.Repositories;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Repository.Settings;
using A360.Security;

namespace A360.Dashboard.Api.IoC;

public static class DashboardServiceCollectionExtensions
{
    public static IServiceCollection AddDashboardApiServices(
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
            "Dashboard",
            serviceProvider.GetRequiredService<ICurrentUserAccessor>()));

        services.AddScoped<DashboardRepository>();
        services.AddScoped<IDashboardRepository>(serviceProvider => serviceProvider.GetRequiredService<DashboardRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<DashboardRepository>());

        services.AddScoped<WidgetRepository>();
        services.AddScoped<IWidgetRepository>(serviceProvider => serviceProvider.GetRequiredService<WidgetRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<WidgetRepository>());

        services.AddScoped<IAddWidgetRepository, AddWidgetRepository>();

        services.AddScoped<INetworkStatusRepository, NetworkStatusRepository>();
        services.AddScoped<IDashboardAssetRepository, DashboardAssetRepository>();

        services.AddHostedService<MongoIndexHostedService>();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "A360 Dashboard API",
                Version = "v1"
            });
        });

        return services;
    }
}
