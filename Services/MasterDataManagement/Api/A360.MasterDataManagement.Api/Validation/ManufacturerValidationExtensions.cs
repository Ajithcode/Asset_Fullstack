using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class ManufacturerValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateManufacturerRequest request)
    {
        return ValidateFields(
            request.ManufacturerName, request.Code, request.Website, request.Email, request.PhoneNumber, request.Models);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateManufacturerRequest request)
    {
        return ValidateFields(
            request.ManufacturerName, request.Code, request.Website, request.Email, request.PhoneNumber, request.Models);
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
}
