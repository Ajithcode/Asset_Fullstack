using System.Net;
using A360.Administration.Domain.Entities;
using A360.Email;

namespace A360.Administration.Api.Accounts;

/// <summary>Result of sending an invitation e-mail. <see cref="Sent"/> is false when SMTP is not set up or the send failed.</summary>
public sealed record InvitationMailResult(bool Sent, string? Problem, IReadOnlyList<string> MissingSettings);

/// <summary>
/// Sends the invitation e-mail through the SMTP server in the "Smtp" settings section. When SMTP is not configured
/// nothing is sent: the e-mail is written to the log and the caller is told which settings are missing, so the
/// administrator can copy the invite link instead of the app pretending the mail went out.
/// </summary>
public sealed class InvitationMailer(IConfiguration configuration, ILogger<InvitationMailer> logger)
{
    /// <summary>Environment-variable names of every Smtp setting that is needed.</summary>
    private static readonly string[] RequiredSettings = ["Host", "Port", "Username", "Password", "FromAddress"];

    public string BaseUrl => (configuration["Invitations:BaseUrl"] ?? "https://a360demo.purpleiq.ai").TrimEnd('/');

    public static int ExpiryDays(IConfiguration configuration) =>
        int.TryParse(configuration["Invitations:ExpiryDays"], out var days) && days > 0 ? days : 7;

    public string LinkFor(string token) => $"{BaseUrl}/accept-invite?token={Uri.EscapeDataString(token)}";

    public IReadOnlyList<string> MissingSettings() =>
        RequiredSettings
            .Where(name => string.IsNullOrWhiteSpace(configuration[$"{SmtpSettings.SectionName}:{name}"]) || (name == "Port" && !(int.TryParse(configuration[$"{SmtpSettings.SectionName}:Port"], out var port) && port > 0)))
            .Select(name => $"{SmtpSettings.SectionName}__{name}")
            .ToList();

    public async Task<InvitationMailResult> SendAsync(User user, string link, DateTime expiresAt, string invitedBy, CancellationToken cancellationToken)
    {
        var subject = "You're invited to Asset360";
        var name = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(user.Name) ? user.Email : user.Name);
        var body =
            $"<p>Hello {name},</p>" +
            $"<p>{WebUtility.HtmlEncode(invitedBy)} has invited you to Asset360. Use the secure link below to choose your password; " +
            "the first time you sign in you will also set up the Google Authenticator app.</p>" +
            $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Accept your invitation</a></p>" +
            $"<p>The link works once and expires on {expiresAt:dd MMM yyyy HH:mm} UTC. If you were not expecting this, ignore this e-mail.</p>";

        var missing = MissingSettings();
        if (missing.Count > 0)
        {
            // The link carries the one-time token, so it is never written to the log; the administrator gets it in the response.
            logger.LogWarning(
                "SMTP is not configured (missing: {Missing}); invitation e-mail to {To} not sent.",
                string.Join(", ", missing), user.Email);
            return new InvitationMailResult(false, "E-mail is not set up on this server.", missing);
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
            await new EmailService(settings).SendEmailAsync(user.Email, subject, body, cancellationToken, isHtml: true);
            return new InvitationMailResult(true, null, []);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Invitation e-mail to {To} failed.", user.Email);
            return new InvitationMailResult(false, "The e-mail server refused or could not be reached.", []);
        }
    }
}
