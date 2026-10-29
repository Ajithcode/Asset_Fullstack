using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class CertificationTypeValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateCertificationTypeRequest request)
    {
        return ValidateFields(request.CertificationTypeName, request.Code, request.Category, request.ApplicableTo, request.ValidityMonths, request.RenewalReminderDays);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateCertificationTypeRequest request)
    {
        return ValidateFields(request.CertificationTypeName, request.Code, request.Category, request.ApplicableTo, request.ValidityMonths, request.RenewalReminderDays);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateCertificationTypeStatusRequest request)
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
        string? certificationTypeName,
        string? code,
        string? category,
        List<string>? applicableTo,
        int? validityMonths,
        int? renewalReminderDays)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(certificationTypeName))
        {
            errors["CertificationTypeName"] =
                ["CertificationTypeName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            errors["Category"] =
                ["Category is required"];
        }

        if (applicableTo?.Any(x => CertificationApplicability.Normalize(x) is null) == true)
        {
            errors["ApplicableTo"] =
                [$"ApplicableTo may only contain: {string.Join(", ", CertificationApplicability.All)}"];
        }

        if (validityMonths < 1)
        {
            errors["ValidityMonths"] =
                ["ValidityMonths must be at least 1"];
        }

        if (renewalReminderDays < 0)
        {
            errors["RenewalReminderDays"] =
                ["RenewalReminderDays cannot be negative"];
        }

        return errors;
    }
}
