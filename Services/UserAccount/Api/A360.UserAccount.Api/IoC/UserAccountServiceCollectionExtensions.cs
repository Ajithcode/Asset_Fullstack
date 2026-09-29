using Microsoft.OpenApi;
using MongoDB.Driver;
using A360.Email;
using A360.Repository.Activity;
using A360.Repository.Repositories;
using A360.Repository.Settings;
using A360.Security;
using A360.UserAccount.Api.Email;
using A360.UserAccount.Api.Security;
using A360.UserAccount.Repository.Repositories;

namespace A360.UserAccount.Api.IoC;

public static class UserAccountServiceCollectionExtensions
{
    public static IServiceCollection AddUserAccountApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
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

        services.AddSingleton<PasswordHashingService>();
        services.AddSingleton<OneTimeCodeService>();
        services.AddSingleton<TokenService>();
        services.AddA360JwtAuthentication(configuration);
        services.AddOtpEmailSender(configuration, environment);

        services.AddScoped<EventLogRepository>();
        services.AddScoped<IEventLogRepository>(serviceProvider => serviceProvider.GetRequiredService<EventLogRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<EventLogRepository>());
        services.AddScoped<IEventLogger>(serviceProvider => new EventLogger(
            serviceProvider.GetRequiredService<IEventLogRepository>(),
            "UserAccount",
            serviceProvider.GetRequiredService<ICurrentUserAccessor>()));

        services.AddScoped<UserRepository>();
        services.AddScoped<IUserRepository>(serviceProvider => serviceProvider.GetRequiredService<UserRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<UserRepository>());

        services.AddScoped<RoleRepository>();
        services.AddScoped<IRoleRepository>(serviceProvider => serviceProvider.GetRequiredService<RoleRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<RoleRepository>());

        services.AddScoped<RefreshTokenRepository>();
        services.AddScoped<IRefreshTokenRepository>(serviceProvider => serviceProvider.GetRequiredService<RefreshTokenRepository>());
        services.AddScoped<IMongoIndexConfigurator>(serviceProvider => serviceProvider.GetRequiredService<RefreshTokenRepository>());

        services.AddHostedService<MongoIndexHostedService>();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Access token returned by POST /api/auth/verify-otp."
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        });

        return services;
    }

    private static void AddOtpEmailSender(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (!string.IsNullOrWhiteSpace(configuration[$"{SmtpSettings.SectionName}:Host"]))
        {
            services.AddEmailServices(configuration);
        }
        else if (environment.IsDevelopment())
        {
            services.AddScoped<IEmailService, LoggingEmailService>();
        }
        else
        {
            services.AddScoped<IEmailService, UnconfiguredEmailService>();
        }
    }
}
