using System.Globalization;
using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class SystemSettingsValidationExtensions
{
    private const int MaxRecordsPerPage = 500;

    public static Dictionary<string, string[]> Validate(
        this UpdateSystemSettingsRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.General is null)
        {
            errors["General"] =
                ["Send at least one settings section, e.g. General"];

            return errors;
        }

        ValidateGeneral(errors, request.General);

        return errors;
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
