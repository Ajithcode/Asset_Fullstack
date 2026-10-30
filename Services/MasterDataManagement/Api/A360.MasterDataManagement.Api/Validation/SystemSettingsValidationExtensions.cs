using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;
using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class SystemSettingsValidationExtensions
{
    private const int MaxRecordsPerPage = 500;

    private static readonly Regex HexColor = new(
        "^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$",
        RegexOptions.Compiled);

    // IPv4 address with an optional /bits mask.
    private static readonly Regex Ipv4Range = new(
        @"^((25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(25[0-5]|2[0-4]\d|1?\d?\d)(/([0-9]|[12]\d|3[0-2]))?$",
        RegexOptions.Compiled);

    public static Dictionary<string, string[]> Validate(
        this UpdateSystemSettingsRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        object?[] sections =
        [
            request.General, request.DateAndTime, request.Branding, request.FileUpload, request.Session, request.Defaults,
            request.Security, request.Email, request.Localization, request.Numbering, request.Integration, request.Backup, request.Other
        ];

        if (sections.All(x => x is null))
        {
            errors["General"] =
                ["Send at least one settings section, e.g. General"];

            return errors;
        }

        if (request.General is not null)
        {
            ValidateGeneral(errors, request.General);
        }

        ValidateOtherSections(errors, request);

        return errors;
    }

    // Same rules as the System Settings page.
    private static void ValidateOtherSections(
        Dictionary<string, string[]> errors,
        UpdateSystemSettingsRequest request)
    {
        void Between(string key, int value, int min, int max)
        {
            if (value < min || value > max)
            {
                errors[key] = [$"{key.Split('.')[^1]} must be between {min} and {max}"];
            }
        }

        if (request.DateAndTime is { } dateTime
            && string.CompareOrdinal(dateTime.WorkdayEnd, dateTime.WorkdayStart) <= 0)
        {
            errors["DateTime.WorkdayEnd"] = ["WorkdayEnd must be after WorkdayStart"];
        }

        if (request.Branding is { } branding && !HexColor.IsMatch(branding.PrimaryColor ?? string.Empty))
        {
            errors["Branding.PrimaryColor"] = ["PrimaryColor must be a hex colour such as #4F2BD8"];
        }

        if (request.FileUpload is { } fileUpload)
        {
            Between("FileUpload.MaxSizeMb", fileUpload.MaxSizeMb, 1, 100);

            if (fileUpload.AllowedTypes is not { Count: > 0 })
            {
                errors["FileUpload.AllowedTypes"] = ["Allow at least one file type"];
            }
        }

        if (request.Session is { } session)
        {
            Between("Session.TimeoutMinutes", session.TimeoutMinutes, 5, 480);
            Between("Session.RememberMeDays", session.RememberMeDays, 0, 90);

            if (session.IdleWarningMinutes < 1 || session.IdleWarningMinutes >= session.TimeoutMinutes)
            {
                errors["Session.IdleWarningMinutes"] = ["IdleWarningMinutes must be at least 1 and less than TimeoutMinutes"];
            }
        }

        if (request.Security is { } security)
        {
            Between("Security.MinPasswordLength", security.MinPasswordLength, 6, 64);
            Between("Security.PasswordExpiryDays", security.PasswordExpiryDays, 0, 365);
            Between("Security.LockoutAttempts", security.LockoutAttempts, 3, 10);

            var badIp = (security.IpAllowlist ?? string.Empty)
                .Split(new[] { '\n', ',' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(x => !Ipv4Range.IsMatch(x));

            if (badIp is not null)
            {
                errors["Security.IpAllowlist"] = [$"{badIp} is not an IPv4 address or range"];
            }
        }

        if (request.Email is { } email)
        {
            Between("Email.SmtpPort", email.SmtpPort, 1, 65535);

            if (email.Channels?.Contains("Email") == true && string.IsNullOrWhiteSpace(email.SmtpHost))
            {
                errors["Email.SmtpHost"] = ["SmtpHost is required while email notifications are on"];
            }

            if (!IsEmail(email.SenderEmail))
            {
                errors["Email.SenderEmail"] = ["SenderEmail is not a valid email address"];
            }
        }

        if (request.Numbering is { } numbering)
        {
            if (!Regex.IsMatch(numbering.Prefix ?? string.Empty, "^[A-Za-z0-9]{1,6}$"))
            {
                errors["Numbering.Prefix"] = ["Prefix must be 1 to 6 letters or digits"];
            }

            if (!Regex.IsMatch(numbering.Pattern ?? string.Empty, @"\{0+\}"))
            {
                errors["Numbering.Pattern"] = ["Pattern must include a running number, e.g. {0000}"];
            }

            Between("Numbering.Digits", numbering.Digits, 3, 8);
            Between("Numbering.StartNumber", numbering.StartNumber, 1, 99999999);
        }

        if (request.Integration is { } integration)
        {
            if (!string.IsNullOrWhiteSpace(integration.WebhookUrl)
                && !integration.WebhookUrl.Trim().StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                errors["Integration.WebhookUrl"] = ["WebhookUrl must use https://"];
            }

            Between("Integration.RateLimitPerMinute", integration.RateLimitPerMinute, 10, 10000);
            Between("Integration.SyncIntervalMinutes", integration.SyncIntervalMinutes, 5, 1440);
        }

        if (request.Backup is { } backup)
        {
            Between("Backup.RetentionDays", backup.RetentionDays, 7, 365);
            Between("Backup.AuditRetentionMonths", backup.AuditRetentionMonths, 6, 120);
            Between("Backup.DeletedRetentionDays", backup.DeletedRetentionDays, 0, 365);
        }

        if (request.Other is { } other)
        {
            if (!string.IsNullOrWhiteSpace(other.HelpdeskEmail) && !IsEmail(other.HelpdeskEmail))
            {
                errors["Other.HelpdeskEmail"] = ["HelpdeskEmail is not a valid email address"];
            }

            if (!string.IsNullOrWhiteSpace(other.TermsUrl)
                && !Uri.TryCreate(other.TermsUrl.Trim(), UriKind.Absolute, out _))
            {
                errors["Other.TermsUrl"] = ["TermsUrl must be a full address"];
            }
        }
    }

    private static bool IsEmail(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && MailAddress.TryCreate(value.Trim(), out var address)
            && address.Address == value.Trim();
    }

    private static void ValidateGeneral(
        Dictionary<string, string[]> errors,
        GeneralSettingsDto general)
    {
        if (string.IsNullOrWhiteSpace(general.SystemName))
        {
            errors["General.SystemName"] =
                ["SystemName is required"];
        }

        if (!string.IsNullOrWhiteSpace(general.SystemUrl)
            && !(Uri.TryCreate(general.SystemUrl.Trim(), UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
        {
            errors["General.SystemUrl"] =
                ["SystemUrl must be a full http:// or https:// address"];
        }

        if (string.IsNullOrWhiteSpace(general.DefaultTimeZone)
            || !TimeZoneInfo.TryFindSystemTimeZoneById(general.DefaultTimeZone.Trim(), out _))
        {
            errors["General.DefaultTimeZone"] =
                ["DefaultTimeZone must be a time zone id such as Asia/Dubai"];
        }

        if (string.IsNullOrWhiteSpace(general.DateFormat) || !IsDateFormat(general.DateFormat.Trim()))
        {
            errors["General.DateFormat"] =
                ["DateFormat must be a date pattern such as dd-MMM-yyyy"];
        }

        if (TimeFormats.Normalize(general.TimeFormat) is null)
        {
            errors["General.TimeFormat"] =
                [$"TimeFormat must be one of: {string.Join(", ", TimeFormats.All)}"];
        }

        if (string.IsNullOrWhiteSpace(general.DefaultLanguage) || !IsLanguage(general.DefaultLanguage.Trim()))
        {
            errors["General.DefaultLanguage"] =
                ["DefaultLanguage must be a language code such as en or ar"];
        }

        if (string.IsNullOrWhiteSpace(general.DefaultCurrency)
            || general.DefaultCurrency.Trim().Length != 3
            || !general.DefaultCurrency.Trim().All(char.IsAsciiLetter))
        {
            errors["General.DefaultCurrency"] =
                ["DefaultCurrency must be a 3-letter currency code such as AED"];
        }

        if (general.RecordsPerPage is null or < 1 or > MaxRecordsPerPage)
        {
            errors["General.RecordsPerPage"] =
                [$"RecordsPerPage must be between 1 and {MaxRecordsPerPage}"];
        }
    }

    // A pattern is accepted when it contains at least a day, month or year part and formats without error.
    private static bool IsDateFormat(string format)
    {
        if (!format.Any(c => c is 'd' or 'M' or 'y'))
        {
            return false;
        }

        try
        {
            _ = new DateTime(2026, 10, 26).ToString(format, CultureInfo.InvariantCulture);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsLanguage(string code)
    {
        try
        {
            return !CultureInfo.GetCultureInfo(code, predefinedOnly: true).Equals(CultureInfo.InvariantCulture);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}
