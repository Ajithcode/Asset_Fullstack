namespace A360.Email;

public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; }

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string FromAddress { get; init; } = string.Empty;

    public string FromName { get; init; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException("SMTP host is required.");
        }

        if (Port <= 0)
        {
            throw new InvalidOperationException("SMTP port is required.");
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            throw new InvalidOperationException("SMTP username is required.");
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            throw new InvalidOperationException("SMTP password is required.");
        }

        if (string.IsNullOrWhiteSpace(FromAddress))
        {
            throw new InvalidOperationException("SMTP from address is required.");
        }
    }
}
