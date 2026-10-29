using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class SupplierValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateSupplierRequest request)
    {
        return ValidateFields(request.SupplierName, request.Code, request.Website, request.Email, request.Phone);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateSupplierRequest request)
    {
        return ValidateFields(request.SupplierName, request.Code, request.Website, request.Email, request.Phone);
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
        string? phone)
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

        return errors;
    }
}
