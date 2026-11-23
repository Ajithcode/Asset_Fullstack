using A360.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace A360.ReportAnalytics.Api.Scheduling;

/// <summary>Sends a report file to its recipients through the SMTP server in the "Smtp" settings section.</summary>
public interface IReportMailer
{
    /// <summary>Null when the SMTP settings are complete; otherwise what is missing.</summary>
    string? ConfigurationProblem { get; }

    Task SendAsync(IReadOnlyCollection<string> recipients, string subject, string htmlBody, ReportFile attachment, CancellationToken cancellationToken);
}

public sealed class ReportMailer(SmtpSettings settings) : IReportMailer
{
    public string? ConfigurationProblem
    {
        get
        {
            try
            {
                settings.Validate();
                return null;
            }
            catch (InvalidOperationException exception)
            {
                return $"Email is not set up for the Reports service: {exception.Message} (fill the \"Smtp\" section of its settings)";
            }
        }
    }

    public async Task SendAsync(IReadOnlyCollection<string> recipients, string subject, string htmlBody, ReportFile attachment, CancellationToken cancellationToken)
    {
        if (ConfigurationProblem is { } problem)
        {
            throw new InvalidOperationException(problem);
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        foreach (var recipient in recipients)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
        }

        message.Subject = subject;

        var body = new BodyBuilder { HtmlBody = htmlBody };
        body.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
        await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    /// <summary>Reads the "Smtp" section without failing when it is empty, so the service still starts; runs then fail with a clear error.</summary>
    public static SmtpSettings ReadSettings(IConfiguration configuration)
    {
        var section = configuration.GetSection(SmtpSettings.SectionName);
        return new SmtpSettings
        {
            Host = section["Host"] ?? string.Empty,
            Port = int.TryParse(section["Port"], out var port) ? port : 0,
            Username = section["Username"] ?? string.Empty,
            Password = section["Password"] ?? string.Empty,
            FromAddress = section["FromAddress"] ?? string.Empty,
            FromName = section["FromName"] ?? "Asset360"
        };
    }
}
