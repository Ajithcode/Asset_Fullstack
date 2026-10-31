using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace A360.Email;

public interface IEmailService
{
    Task SendEmailAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default,
        bool isHtml = false);
}

public sealed class EmailService : IEmailService
{
    private readonly SmtpSettings _settings;

    public EmailService(SmtpSettings settings)
    {
        _settings = settings;
    }

    public async Task SendEmailAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default,
        bool isHtml = false)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(new MailboxAddress("", to));
        message.Subject = subject;
        message.Body = new TextPart(isHtml ? "html" : "plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
