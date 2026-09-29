using MongoDB.Driver;
using A360.Asset.Repository.Repositories;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Repository.Settings;
using A360.Security;

namespace A360.Asset.Api.IoC;

public static class AssetServiceCollectionExtensions
{
    public static IServiceCollection AddAssetApiServices(
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
            "Asset",
            serviceProvider.GetRequiredService<ICurrentUserAccessor>()));

        services.AddScoped<AssetRepository>();
        services.AddScoped<IAssetRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetRepository>());

        services.AddScoped<AssetLocationRepository>();
        services.AddScoped<IAssetLocationRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetLocationRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetLocationRepository>());

        services.AddScoped<AssetOwnershipRepository>();
        services.AddScoped<IAssetOwnershipRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetOwnershipRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetOwnershipRepository>());

        services.AddScoped<AssetLifecycleRepository>();
        services.AddScoped<IAssetLifecycleRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetLifecycleRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetLifecycleRepository>());

        services.AddScoped<AssetTrackingAndTelemetryRepository>();
        services.AddScoped<IAssetTrackingAndTelemetryRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetTrackingAndTelemetryRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetTrackingAndTelemetryRepository>());

        services.AddScoped<AssetMaintenanceAndServiceRepository>();
        services.AddScoped<IAssetMaintenanceAndServiceRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetMaintenanceAndServiceRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetMaintenanceAndServiceRepository>());

        services.AddScoped<AssetUtilizationAndPerformanceRepository>();
        services.AddScoped<IAssetUtilizationAndPerformanceRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetUtilizationAndPerformanceRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetUtilizationAndPerformanceRepository>());

        services.AddScoped<AssetFinancialDetailsRepository>();
        services.AddScoped<IAssetFinancialDetailsRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetFinancialDetailsRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetFinancialDetailsRepository>());

        services.AddScoped<AssetDocumentsRepository>();
        services.AddScoped<IAssetDocumentsRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetDocumentsRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetDocumentsRepository>());

        services.AddScoped<AssetContractRepository>();
        services.AddScoped<IAssetContractRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetContractRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetContractRepository>());

        services.AddScoped<AssetIncidentRepository>();
        services.AddScoped<IAssetIncidentRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetIncidentRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetIncidentRepository>());

        services.AddScoped<AssetAuditAndVerificationRepository>();
        services.AddScoped<IAssetAuditAndVerificationRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetAuditAndVerificationRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetAuditAndVerificationRepository>());

        services.AddScoped<AssetActivityRepository>();
        services.AddScoped<IAssetActivityRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetActivityRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetActivityRepository>());

        services.AddScoped<AssetDomainRepository>();
        services.AddScoped<IAssetDomainRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetDomainRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetDomainRepository>());

        services.AddScoped<AssetIntegrationRepository>();
        services.AddScoped<IAssetIntegrationRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetIntegrationRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetIntegrationRepository>());

        services.AddScoped<AssetCertificationRepository>();
        services.AddScoped<IAssetCertificationRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetCertificationRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetCertificationRepository>());

        services.AddScoped<AssetAuditRepository>();
        services.AddScoped<IAssetAuditRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetAuditRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetAuditRepository>());

        services.AddScoped<AssetMovementRepository>();
        services.AddScoped<IAssetMovementRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetMovementRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetMovementRepository>());

        services.AddScoped<AssetDisposalRepository>();
        services.AddScoped<IAssetDisposalRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetDisposalRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetDisposalRepository>());

        services.AddScoped<AssetCheckoutRepository>();
        services.AddScoped<IAssetCheckoutRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetCheckoutRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetCheckoutRepository>());

        services.AddScoped<AssetCheckinRepository>();
        services.AddScoped<IAssetCheckinRepository>(serviceProvider => serviceProvider.GetRequiredService<AssetCheckinRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<AssetCheckinRepository>());

        services.AddScoped<TaggedAssetsRepository>();
        services.AddScoped<ITaggedAssetsRepository>(serviceProvider => serviceProvider.GetRequiredService<TaggedAssetsRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<TaggedAssetsRepository>());

        services.AddHostedService<MongoIndexHostedService>();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }
}
