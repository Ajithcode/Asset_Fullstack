using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

/// <summary>
/// Checks shared by the vendor masters (suppliers, service providers and AMC vendors).
/// </summary>
internal static class VendorValidation
{
    public static void ValidateWebsite(
        Dictionary<string, string[]> errors,
        string? website)
    {
        if (!string.IsNullOrWhiteSpace(website)
            && !(Uri.TryCreate(website.Trim(), UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
        {
            errors["Website"] =
                ["Website must be a full http:// or https:// address"];
        }
    }

    public static void ValidateExtras(
        Dictionary<string, string[]> errors,
        string? alternateEmail,
        string? alternatePhone,
        List<VendorDocumentDto>? documents)
    {
        var contactErrors = new Dictionary<string, string[]>();
        ContactValidation.ValidateEmailAndPhone(contactErrors, alternateEmail, alternatePhone, "AlternatePhone");

        if (contactErrors.Remove("Email", out var emailError))
        {
            errors["AlternateEmail"] = emailError.Select(x => x.Replace("Email", "AlternateEmail")).ToArray();
        }

        foreach (var (key, value) in contactErrors)
        {
            errors[key] = value;
        }

        if (documents is not null && documents.Any(x => x is null || string.IsNullOrWhiteSpace(x.Name)))
        {
            errors["Documents"] =
                ["Every document needs a name"];
        }
    }

    public static void ValidateRange(
        Dictionary<string, string[]> errors,
        string field,
        decimal? value,
        decimal min,
        decimal? max = null)
    {
        if (value is null)
        {
            return;
        }

        if (value < min || value > max)
        {
            errors[field] = max is null
                ? [$"{field} must be {min} or more"]
                : [$"{field} must be between {min} and {max}"];
        }
    }
}
