using A360.Email;
using A360.Tracking.Domain.Entities;
using MongoDB.Driver;

namespace A360.Tracking.Api.Alerts;

/// <summary>
/// E-mail channel of an alert rule. Ready for SMTP: it sends as soon as the "Smtp" settings are set
/// (Smtp__Host, Smtp__Port, Smtp__Username, Smtp__Password, Smtp__FromAddress, optional Smtp__FromName) and
/// Alerts__EmailTo lists the recipients (comma separated). Without them nothing is sent and nothing pretends it was:
/// the alert's history records "Email not sent" with the missing keys.
/// </summary>
public sealed class AlertEmailNotifier(IConfiguration configuration, IMongoDatabase database, ILogger<AlertEmailNotifier> logger)
{
    private static readonly string[] Required = ["Host", "Port", "Username", "Password", "FromAddress"];

    /// <summary>The Smtp__* keys still missing (empty = e-mail can be sent).</summary>
    public IReadOnlyList<string> MissingSettings() =>
        Required
            .Where(name => string.IsNullOrWhiteSpace(configuration[$"{SmtpSettings.SectionName}:{name}"])
                           || (name == "Port" && !(int.TryParse(configuration[$"{SmtpSettings.SectionName}:Port"], out var port) && port > 0)))
            .Select(name => $"{SmtpSettings.SectionName}__{name}")
            .ToList();

    public bool Configured => MissingSettings().Count == 0;

    public async Task SendAsync(IoTAlert alert, AlertRule rule, CancellationToken ct)
    {
        if (!rule.NotificationChannels.Contains(AlertNotificationChannel.Email))
        {
            return;
        }

        var missing = MissingSettings();
        var to = (configuration["Alerts:EmailTo"] ?? string.Empty).Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (missing.Count > 0 || to.Length == 0)
        {
            var why = missing.Count > 0 ? $"SMTP not configured (missing {string.Join(", ", missing)})" : "no recipients (Alerts__EmailTo)";
            logger.LogWarning("{AlertId}: e-mail not sent: {Why}", alert.AlertId, why);
            await NoteAsync(alert, $"Email not sent: {why}", ct);
            return;
        }

        try
        {
            var settings = new SmtpSettings
            {
                Host = configuration[$"{SmtpSettings.SectionName}:Host"]!,
                Port = int.Parse(configuration[$"{SmtpSettings.SectionName}:Port"]!),
                Username = configuration[$"{SmtpSettings.SectionName}:Username"]!,
                Password = configuration[$"{SmtpSettings.SectionName}:Password"]!,
                FromAddress = configuration[$"{SmtpSettings.SectionName}:FromAddress"]!,
                FromName = configuration[$"{SmtpSettings.SectionName}:FromName"] ?? "Asset360"
            };
            var subject = $"[{rule.Severity}] {alert.EventType}: {alert.AssetName}";
            var body = $"<p><b>{System.Net.WebUtility.HtmlEncode(alert.EventType)}</b> · {System.Net.WebUtility.HtmlEncode(alert.AssetName)}</p><p>{System.Net.WebUtility.HtmlEncode(alert.Message ?? string.Empty)}</p><p>{alert.AlertId} · {alert.RaisedAt:dd MMM yyyy HH:mm} UTC</p>";
            foreach (var recipient in to)
            {
                await new EmailService(settings).SendEmailAsync(recipient, subject, body, ct, isHtml: true);
            }

            await NoteAsync(alert, $"Email sent to {to.Length} recipient(s)", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "{AlertId}: e-mail failed", alert.AlertId);
            await NoteAsync(alert, "Email failed: the mail server refused or could not be reached", ct);
        }
    }

    private Task NoteAsync(IoTAlert alert, string note, CancellationToken ct) =>
        database.GetCollection<IoTAlert>("IoTAlert").UpdateOneAsync(a => a.Id == alert.Id,
            Builders<IoTAlert>.Update.Push(a => a.History, new IoTAlertHistoryEntry { At = DateTime.UtcNow, By = "Email channel", Action = "Notified", Note = note }), cancellationToken: ct);
}
