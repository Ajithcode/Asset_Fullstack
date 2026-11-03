using MongoDB.Driver;
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
