using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class ServiceProviderValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateServiceProviderRequest request)
    {
        return ValidateFields(request.ServiceProviderName, request.Code, request.Website, request.Email, request.Phone);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateServiceProviderRequest request)
    {
        return ValidateFields(request.ServiceProviderName, request.Code, request.Website, request.Email, request.Phone);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateServiceProviderStatusRequest request)
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
        string? serviceProviderName,
        string? code,
        string? website,
        string? email,
        string? phone)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(serviceProviderName))
        {
            errors["ServiceProviderName"] =
                ["ServiceProviderName is required"];
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

        return errors;
    }
}
