
using MongoDB.Driver;
using A360.Workflows.Repository.Repositories;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Repository.Settings;
using A360.Security;

namespace A360.Workflows.Api.IoC;

public static class WorkflowsServiceCollectionExtensions
{
    public static IServiceCollection AddWorkflowsApiServices(
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
            "Workflows",
            serviceProvider.GetRequiredService<ICurrentUserAccessor>()));

        // Workflow List Repository Registration

        services.AddScoped<WorkflowListRepository>();

        services.AddScoped<IWorkflowListRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<WorkflowListRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<WorkflowListRepository>());

        // Workflow Builder Repository Registration

        services.AddScoped<WorkflowBuilderRepository>();

        services.AddScoped<IWorkflowBuilderRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<WorkflowBuilderRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<WorkflowBuilderRepository>());

        // Workflow Instance Repository Registration

        services.AddScoped<WorkflowInstanceRepository>();

        services.AddScoped<IWorkflowInstanceRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<WorkflowInstanceRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<WorkflowInstanceRepository>());

        // Workflow Approval Task Repository Registration

        services.AddScoped<WorkflowApprovalTaskRepository>();

        services.AddScoped<IWorkflowApprovalTaskRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<WorkflowApprovalTaskRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<WorkflowApprovalTaskRepository>());

        // Workflow Insight Repository Registration

        services.AddScoped<WorkflowInsightRepository>();

        services.AddScoped<IWorkflowInsightRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<WorkflowInsightRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<WorkflowInsightRepository>());

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
                    Title = "A360 Workflows API",
                    Version = "v1"
                });
        });

        return services;
    }
}
