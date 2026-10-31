using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace A360.Email;

public static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddEmailServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var smtpSettings = new SmtpSettings
        {
            Host = configuration[$"{SmtpSettings.SectionName}:Host"] ?? string.Empty,
            Port = int.TryParse(configuration[$"{SmtpSettings.SectionName}:Port"], out var smtpPort) ? smtpPort : 0,
            Username = configuration[$"{SmtpSettings.SectionName}:Username"] ?? string.Empty,
            Password = configuration[$"{SmtpSettings.SectionName}:Password"] ?? string.Empty,
            FromAddress = configuration[$"{SmtpSettings.SectionName}:FromAddress"] ?? string.Empty,
            FromName = configuration[$"{SmtpSettings.SectionName}:FromName"] ?? string.Empty
        };

        smtpSettings.Validate();

        services.AddSingleton(smtpSettings);

        services.AddScoped<IEmailService, EmailService>();

        return services;
    }
}
