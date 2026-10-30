using A360.Inspection.Api.Contracts;

namespace A360.Inspection.Api.Validation;

public static class SignatureAndStampValidationExtensions
{
    public static Dictionary<string, string[]> Validate(this CreateSignatureAndStampRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.User))
        {
            errors["User"] = ["User is required"];
        }

        if (string.IsNullOrWhiteSpace(request.SignatureName))
        {
            errors["SignatureName"] = ["SignatureName is required"];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(this UpdateSignatureAndStampRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.User))
        {
            errors["User"] = ["User is required"];
        }

        if (string.IsNullOrWhiteSpace(request.SignatureName))
        {
            errors["SignatureName"] = ["SignatureName is required"];
        }

        return errors;
    }
}
