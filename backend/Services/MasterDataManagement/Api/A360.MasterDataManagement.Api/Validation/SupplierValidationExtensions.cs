using System.Text.RegularExpressions;
using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class SupplierValidationExtensions
{
    // Two letters, two check digits, then 10 to 32 letters, digits or spaces.
    private static readonly Regex IbanPattern = new(
        "^[A-Za-z]{2}[0-9]{2}[A-Za-z0-9 ]{10,32}$",
        RegexOptions.Compiled);

    public static Dictionary<string, string[]> Validate(
        this CreateSupplierRequest request)
    {
        return ValidateFields(request.SupplierName, request.Code, request.Website, request.Email, request.Phone,
            request.AlternateEmail, request.AlternatePhone, request.Documents, request.Rating, request.CreditLimit, request.Iban);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateSupplierRequest request)
    {
        return ValidateFields(request.SupplierName, request.Code, request.Website, request.Email, request.Phone,
            request.AlternateEmail, request.AlternatePhone, request.Documents, request.Rating, request.CreditLimit, request.Iban);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateSupplierStatusRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            errors["Status"] =
                ["Status is required"];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateFields(
        string? supplierName,
        string? code,
        string? website,
        string? email,
        string? phone,
        string? alternateEmail,
        string? alternatePhone,
        List<VendorDocumentDto>? documents,
        decimal? rating,
        decimal? creditLimit,
        string? iban)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(supplierName))
        {
            errors["SupplierName"] =
                ["SupplierName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (!string.IsNullOrWhiteSpace(website)
            && !(Uri.TryCreate(website.Trim(), UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
        {
            errors["Website"] =
                ["Website must be a full http:// or https:// address"];
        }

        ContactValidation.ValidateEmailAndPhone(errors, email, phone, "Phone");

        VendorValidation.ValidateExtras(errors, alternateEmail, alternatePhone, documents);
        VendorValidation.ValidateRange(errors, "Rating", rating, 0, 5);
        VendorValidation.ValidateRange(errors, "CreditLimit", creditLimit, 0);

        if (!string.IsNullOrWhiteSpace(iban) && !IbanPattern.IsMatch(iban.Trim()))
        {
            errors["Iban"] =
                ["Iban is not a valid IBAN, e.g. AE07 0331 2345 6789 0123 456"];
        }

        return errors;
    }
}
