
using MongoDB.Driver;
using A360.Wip.Repository.Repositories;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Repository.Settings;
using A360.Security;

namespace A360.Wip.Api.IoC;

public static class WipServiceCollectionExtensions
{
    public static IServiceCollection AddWipApiServices(
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
            "Wip",
            serviceProvider.GetRequiredService<ICurrentUserAccessor>()));

        // Job Master Repository Registration

        services.AddScoped<JobMasterRepository>();

        services.AddScoped<IJobMasterRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<JobMasterRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<JobMasterRepository>());

        // Status Master Repository Registration

        services.AddScoped<StatusMasterRepository>();

        services.AddScoped<IStatusMasterRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<StatusMasterRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<StatusMasterRepository>());

        // Resource Master Repository Registration

        services.AddScoped<ResourceMasterRepository>();

        services.AddScoped<IResourceMasterRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<ResourceMasterRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<ResourceMasterRepository>());

        // Task Master Repository Registration

        services.AddScoped<TaskMasterRepository>();

        services.AddScoped<ITaskMasterRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<TaskMasterRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<TaskMasterRepository>());

        // Checklist Master Repository Registration

        services.AddScoped<ChecklistMasterRepository>();

        services.AddScoped<IChecklistMasterRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<ChecklistMasterRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<ChecklistMasterRepository>());

        // Checklist Item Repository Registration

        services.AddScoped<ChecklistItemRepository>();

        services.AddScoped<IChecklistItemRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<ChecklistItemRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<ChecklistItemRepository>());

        // Location Master Repository Registration

        services.AddScoped<LocationMasterRepository>();

        services.AddScoped<ILocationMasterRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<LocationMasterRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<LocationMasterRepository>());

        // Asset Linking Repository Registration

        services.AddScoped<AssetLinkingRepository>();

        services.AddScoped<IAssetLinkingRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<AssetLinkingRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<AssetLinkingRepository>());

        // Sla Master Repository Registration

        services.AddScoped<SlaMasterRepository>();

        services.AddScoped<ISlaMasterRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<SlaMasterRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<SlaMasterRepository>());

        // Issue Master Repository Registration

        services.AddScoped<IssueMasterRepository>();

        services.AddScoped<IIssueMasterRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<IssueMasterRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<IssueMasterRepository>());

        // Material Consumption Repository Registration

        services.AddScoped<MaterialConsumptionRepository>();

        services.AddScoped<IMaterialConsumptionRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<MaterialConsumptionRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<MaterialConsumptionRepository>());

        // Permit Master Repository Registration

        services.AddScoped<PermitMasterRepository>();

        services.AddScoped<IPermitMasterRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<PermitMasterRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<PermitMasterRepository>());

        // Progress Log Repository Registration

        services.AddScoped<ProgressLogRepository>();

        services.AddScoped<IProgressLogRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<ProgressLogRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<ProgressLogRepository>());

        // Alert Master Repository Registration

        services.AddScoped<AlertMasterRepository>();

        services.AddScoped<IAlertMasterRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<AlertMasterRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<AlertMasterRepository>());

        // Kpi Master Repository Registration

        services.AddScoped<KpiMasterRepository>();

        services.AddScoped<IKpiMasterRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<KpiMasterRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<KpiMasterRepository>());

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
                    Title = "A360 WIP API",
                    Version = "v1"
                });
        });

        return services;
    }
}
