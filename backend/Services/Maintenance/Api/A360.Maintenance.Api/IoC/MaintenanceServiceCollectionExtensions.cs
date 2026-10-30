
using MongoDB.Driver;
using A360.Maintenance.Repository.Repositories;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Repository.Settings;
using A360.Security;

namespace A360.Maintenance.Api.IoC;

public static class MaintenanceServiceCollectionExtensions
{
    public static IServiceCollection AddMaintenanceApiServices(
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
            "Maintenance",
            serviceProvider.GetRequiredService<ICurrentUserAccessor>()));

        // Work Order Repository Registration

        services.AddScoped<WorkOrderRepository>();

        services.AddScoped<IWorkOrderRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<WorkOrderRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<WorkOrderRepository>());

        // Maintenance Task Repository Registration

        services.AddScoped<MaintenanceTaskRepository>();

        services.AddScoped<IMaintenanceTaskRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<MaintenanceTaskRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<MaintenanceTaskRepository>());

        // Preventive Maintenance Repository Registration

        services.AddScoped<PreventiveMaintenanceRepository>();

        services.AddScoped<IPreventiveMaintenanceRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<PreventiveMaintenanceRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<PreventiveMaintenanceRepository>());


        // Predictive Maintenance Repository Registration

        services.AddScoped<PredictiveMaintenanceRepository>();

        services.AddScoped<IPredictiveMaintenanceRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<PredictiveMaintenanceRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<PredictiveMaintenanceRepository>());

        // Issue Report Repository Registration

        services.AddScoped<IssueReportRepository>();

        services.AddScoped<IIssueReportRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<IssueReportRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<IssueReportRepository>());

        // Spare Part Repository Registration

        services.AddScoped<SparePartRepository>();

        services.AddScoped<ISparePartRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<SparePartRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<SparePartRepository>());

        // Technician Repository Registration

        services.AddScoped<TechnicianRepository>();

        services.AddScoped<ITechnicianRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<TechnicianRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<TechnicianRepository>());

        // Vendor Amc Repository Registration

        services.AddScoped<VendorAmcRepository>();

        services.AddScoped<IVendorAmcRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<VendorAmcRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<VendorAmcRepository>());

        // Cost Tracking Repository Registration

        services.AddScoped<CostTrackingRepository>();

        services.AddScoped<ICostTrackingRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<CostTrackingRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<CostTrackingRepository>());

        // Downtime Record Repository Registration

        services.AddScoped<DowntimeRecordRepository>();

        services.AddScoped<IDowntimeRecordRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<DowntimeRecordRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<DowntimeRecordRepository>());

        // Performance Metric Repository Registration

        services.AddScoped<PerformanceMetricRepository>();

        services.AddScoped<IPerformanceMetricRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<PerformanceMetricRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<PerformanceMetricRepository>());

        // Compliance Inspection Repository Registration

        services.AddScoped<ComplianceInspectionRepository>();

        services.AddScoped<IComplianceInspectionRepository>(
            serviceProvider =>
                serviceProvider.GetRequiredService<ComplianceInspectionRepository>());

        services.AddScoped<IMongoIndexConfigurator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<ComplianceInspectionRepository>());

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
                    Title = "A360 Maintenance API",
                    Version = "v1"
                });
        });

        return services;
    }
}
