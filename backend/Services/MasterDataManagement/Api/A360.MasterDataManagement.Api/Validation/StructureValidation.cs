using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;
using A360.MasterDataManagement.Api.Contracts;
using A360.Repository.Repositories;

namespace A360.MasterDataManagement.Api.Validation;

/// <summary>
/// Checks shared by the organization structure masters; the rules match the forms in the UI.
/// </summary>
internal static class StructureValidation
{
    private static readonly Regex WebsitePattern = new(
        @"^https?://[^\s.]+\.\S+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static void ValidateEmail(
        Dictionary<string, string[]> errors,
        string fieldName,
        string? email)
    {
        if (!string.IsNullOrWhiteSpace(email)
            && !(MailAddress.TryCreate(email.Trim(), out var address) && address.Address == email.Trim()))
        {
            errors[fieldName] =
                [$"{fieldName} is not a valid email address"];
        }
    }

    public static void ValidateWebsite(
        Dictionary<string, string[]> errors,
        string? website)
    {
        if (!string.IsNullOrWhiteSpace(website) && !WebsitePattern.IsMatch(website.Trim()))
        {
            errors["Website"] =
                ["Website must be a full address, e.g. https://www.example.com"];
        }
    }

    /// <summary>Dates are 'yyyy-MM-dd'; EffectiveTo is optional but may not be before EffectiveFrom.</summary>
    public static void ValidateEffectiveDates(
        Dictionary<string, string[]> errors,
        string? effectiveFrom,
        string? effectiveTo)
    {
        var from = ParseDate(errors, "EffectiveFrom", effectiveFrom);
        var to = ParseDate(errors, "EffectiveTo", effectiveTo);

        if (from is not null && to is not null && to < from)
        {
            errors["EffectiveTo"] =
                ["EffectiveTo must be on or after EffectiveFrom"];
        }
    }

    /// <summary>A parent in the same collection is always picked by id.</summary>
    public static void ValidateParent(
        Dictionary<string, string[]> errors,
        string fieldName,
        MasterDataReferenceDto? parent)
    {
        if (parent is not null && !MongoObjectId.IsValid(parent.Id ?? string.Empty))
        {
            errors[fieldName] =
                [$"{fieldName} must have a valid id"];
        }
    }

    public static void ValidateNotNegative(
        Dictionary<string, string[]> errors,
        string fieldName,
        decimal? value)
    {
        if (value < 0)
        {
            errors[fieldName] =
                [$"{fieldName} cannot be negative"];
        }
    }

    public static void ValidateCoordinates(
        Dictionary<string, string[]> errors,
        double? latitude,
        double? longitude)
    {
        if (latitude is < -90 or > 90)
        {
            errors["Latitude"] =
                ["Latitude must be between -90 and 90"];
        }

        if (longitude is < -180 or > 180)
        {
            errors["Longitude"] =
                ["Longitude must be between -180 and 180"];
        }
    }

    private static DateOnly? ParseDate(
        Dictionary<string, string[]> errors,
        string fieldName,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        errors[fieldName] =
            [$"{fieldName} must be a date in yyyy-MM-dd format"];
        return null;
    }
}
