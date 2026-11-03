using A360.Email;

namespace A360.UserAccount.Api.Email;

public sealed class EmailNotConfiguredException : InvalidOperationException
{
    public EmailNotConfiguredException()
        : base("SMTP is not configured. Set the 'Smtp' section in appsettings.")
    {
    }
}

/// <summary>
/// Development-only sender used when no SMTP server is configured: writes the email to the log instead.
/// </summary>
public sealed class LoggingEmailService : IEmailService
{
    private readonly ILogger<LoggingEmailService> _logger;

    public LoggingEmailService(ILogger<LoggingEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default,
        bool isHtml = false)
    {
        _logger.LogWarning(
            "SMTP is not configured; email not sent (Development only).\nTo: {To}\nSubject: {Subject}\n{Body}",
            to,
            subject,
            body);

        return Task.CompletedTask;
    }
}

public sealed class UnconfiguredEmailService : IEmailService
{
    public Task SendEmailAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default,
        bool isHtml = false)
    {
        throw new EmailNotConfiguredException();
    }
}
