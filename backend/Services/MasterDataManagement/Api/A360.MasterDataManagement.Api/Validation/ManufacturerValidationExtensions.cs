using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class ManufacturerValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateManufacturerRequest request)
    {
        var errors = ValidateFields(
            request.ManufacturerName, request.Code, request.Website, request.Email, request.PhoneNumber, request.Models);
        ValidateDetails(
            errors, request.SecondaryEmail, request.SupportPortal, request.SupportEmail, request.StandardWarrantyMonths, request.SlaResponseHours);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateManufacturerRequest request)
    {
        var errors = ValidateFields(
            request.ManufacturerName, request.Code, request.Website, request.Email, request.PhoneNumber, request.Models);
        ValidateDetails(
            errors, request.SecondaryEmail, request.SupportPortal, request.SupportEmail, request.StandardWarrantyMonths, request.SlaResponseHours);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateManufacturerStatusRequest request)
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
        string? manufacturerName,
        string? code,
        string? website,
        string? email,
        string? phoneNumber,
        List<MasterDataReferenceDto>? models)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(manufacturerName))
        {
            errors["ManufacturerName"] =
                ["ManufacturerName is required"];
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

        ContactValidation.ValidateEmailAndPhone(errors, email, phoneNumber);

        // Models are asset models in this service, so they are picked by id.
        if (models is not null)
        {
            if (models.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id)))
            {
                errors["Models"] =
                    ["Every entry in Models needs an asset model id"];
            }
            else if (models.GroupBy(x => x.Id!.Trim()).Any(x => x.Count() > 1))
            {
                errors["Models"] =
                    ["Models contains duplicate entries"];
            }
        }

        return errors;
    }

    private static void ValidateDetails(
        Dictionary<string, string[]> errors,
        string? secondaryEmail,
        string? supportPortal,
        string? supportEmail,
        int? standardWarrantyMonths,
        int? slaResponseHours)
    {
        if (!IsEmail(secondaryEmail))
        {
            errors["SecondaryEmail"] =
                ["SecondaryEmail is not a valid email address"];
        }

        if (!IsEmail(supportEmail))
        {
            errors["SupportEmail"] =
                ["SupportEmail is not a valid email address"];
        }

        if (!string.IsNullOrWhiteSpace(supportPortal)
            && !(Uri.TryCreate(supportPortal.Trim(), UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
        {
            errors["SupportPortal"] =
                ["SupportPortal must be a full http:// or https:// address"];
        }

        if (standardWarrantyMonths is < 0 or > 240)
        {
            errors["StandardWarrantyMonths"] =
                ["StandardWarrantyMonths must be between 0 and 240"];
        }

        if (slaResponseHours is < 1 or > 720)
        {
            errors["SlaResponseHours"] =
                ["SlaResponseHours must be between 1 and 720"];
        }
    }

    // Empty counts as valid; the field is optional.
    private static bool IsEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email)
            || (System.Net.Mail.MailAddress.TryCreate(email.Trim(), out var address) && address.Address == email.Trim());
    }
}
