using System.Text.Json.Serialization;
using MongoDB.Driver;
using A360.Inspection.Api.Seed;
using A360.Inspection.Repository.Repositories;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Repository.Sequences;
using A360.Repository.Settings;
using A360.Security;

namespace A360.Inspection.Api.IoC;

public static class InspectionServiceCollectionExtensions
{
    public static IServiceCollection AddInspectionApiServices(
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
            "Inspection",
            serviceProvider.GetRequiredService<ICurrentUserAccessor>()));

        services.AddScoped<InspectionTypeRepository>();
        services.AddScoped<IInspectionTypeRepository>(serviceProvider => serviceProvider.GetRequiredService<InspectionTypeRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<InspectionTypeRepository>());

        services.AddScoped<TaskCategoryRepository>();
        services.AddScoped<ITaskCategoryRepository>(serviceProvider => serviceProvider.GetRequiredService<TaskCategoryRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<TaskCategoryRepository>());

        services.AddScoped<InspectionTaskRepository>();
        services.AddScoped<IInspectionTaskRepository>(serviceProvider => serviceProvider.GetRequiredService<InspectionTaskRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<InspectionTaskRepository>());

        services.AddScoped<FailureReasonRepository>();
        services.AddScoped<IFailureReasonRepository>(serviceProvider => serviceProvider.GetRequiredService<FailureReasonRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<FailureReasonRepository>());

        services.AddScoped<DefectRepository>();
        services.AddScoped<IDefectRepository>(serviceProvider => serviceProvider.GetRequiredService<DefectRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<DefectRepository>());

        services.AddScoped<SeverityRepository>();
        services.AddScoped<ISeverityRepository>(serviceProvider => serviceProvider.GetRequiredService<SeverityRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<SeverityRepository>());

        services.AddScoped<PriorityRepository>();
        services.AddScoped<IPriorityRepository>(serviceProvider => serviceProvider.GetRequiredService<PriorityRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<PriorityRepository>());

        services.AddScoped<SignatureAndStampRepository>();
        services.AddScoped<ISignatureAndStampRepository>(serviceProvider => serviceProvider.GetRequiredService<SignatureAndStampRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<SignatureAndStampRepository>());

        services.AddScoped<NotificationTemplateRepository>();
        services.AddScoped<INotificationTemplateRepository>(serviceProvider => serviceProvider.GetRequiredService<NotificationTemplateRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<NotificationTemplateRepository>());

        services.AddScoped<ReportTemplateRepository>();
        services.AddScoped<IReportTemplateRepository>(serviceProvider => serviceProvider.GetRequiredService<ReportTemplateRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<ReportTemplateRepository>());

        services.AddScoped<NumberingSequenceRepository>();
        services.AddScoped<INumberingSequenceRepository>(serviceProvider => serviceProvider.GetRequiredService<NumberingSequenceRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<NumberingSequenceRepository>());

        services.AddScoped<HolidayAndWorkingCalendarRepository>();
        services.AddScoped<IHolidayAndWorkingCalendarRepository>(serviceProvider => serviceProvider.GetRequiredService<HolidayAndWorkingCalendarRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<HolidayAndWorkingCalendarRepository>());

        services.AddScoped<ChecklistRepository>();
        services.AddScoped<IChecklistRepository>(serviceProvider => serviceProvider.GetRequiredService<ChecklistRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<ChecklistRepository>());

        services.AddScoped<InspectionPlanRepository>();
        services.AddScoped<IInspectionPlanRepository>(serviceProvider => serviceProvider.GetRequiredService<InspectionPlanRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<InspectionPlanRepository>());

        services.AddScoped<InspectionRepository>();
        services.AddScoped<IInspectionRepository>(serviceProvider => serviceProvider.GetRequiredService<InspectionRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<InspectionRepository>());

        services.AddScoped<InspectionFindingRepository>();
        services.AddScoped<IInspectionFindingRepository>(serviceProvider => serviceProvider.GetRequiredService<InspectionFindingRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<InspectionFindingRepository>());

        services.AddScoped<CalibrationInstrumentRepository>();
        services.AddScoped<ICalibrationInstrumentRepository>(serviceProvider => serviceProvider.GetRequiredService<CalibrationInstrumentRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<CalibrationInstrumentRepository>());

        services.AddHostedService<MongoIndexHostedService>();
        services.AddHostedService<InspectionSeedHostedService>();
        // Lets numeric fields arrive as strings from form inputs ("12" as well as 12).
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.NumberHandling = JsonNumberHandling.AllowReadingFromString);
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }
}
