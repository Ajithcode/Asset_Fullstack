using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class AmcVendorValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateAmcVendorRequest request)
    {
        return ValidateFields(
            request.AmcVendorName, request.Code, request.StartDate, request.EndDate, request.AssetType, request.Email,
            request.Website, request.Phone, request.AlternateEmail, request.AlternatePhone, request.Documents,
            request.ContractValue, request.RenewalNoticeDays, request.VisitsPerYear, request.ResponseHours,
            request.ResolutionHours, request.UptimePercent, request.PenaltyPerHour);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateAmcVendorRequest request)
    {
        return ValidateFields(
            request.AmcVendorName, request.Code, request.StartDate, request.EndDate, request.AssetType, request.Email,
            request.Website, request.Phone, request.AlternateEmail, request.AlternatePhone, request.Documents,
            request.ContractValue, request.RenewalNoticeDays, request.VisitsPerYear, request.ResponseHours,
            request.ResolutionHours, request.UptimePercent, request.PenaltyPerHour);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateAmcVendorStatusRequest request)
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
        string? amcVendorName,
        string? code,
        DateOnly? startDate,
        DateOnly? endDate,
        MasterDataReferenceDto? assetType,
        string? email,
        string? website,
        string? phone,
        string? alternateEmail,
        string? alternatePhone,
        List<VendorDocumentDto>? documents,
        decimal? contractValue,
        int? renewalNoticeDays,
        int? visitsPerYear,
        int? responseHours,
        int? resolutionHours,
        decimal? uptimePercent,
        decimal? penaltyPerHour)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(amcVendorName))
        {
            errors["AmcVendorName"] =
                ["AmcVendorName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        if (startDate is null)
        {
            errors["StartDate"] =
                ["StartDate is required (yyyy-MM-dd)"];
        }

        if (endDate is null)
        {
            errors["EndDate"] =
                ["EndDate is required (yyyy-MM-dd)"];
        }
        else if (startDate is not null && endDate < startDate)
        {
            errors["EndDate"] =
                ["EndDate cannot be before StartDate"];
        }

        // The asset type is a record in this service and is normally picked by id; a name alone is kept as typed.
        if (assetType is not null && string.IsNullOrWhiteSpace(assetType.Id) && string.IsNullOrWhiteSpace(assetType.Name))
        {
            errors["AssetType"] =
                ["AssetType must have an asset type id or name"];
        }

        ContactValidation.ValidateEmailAndPhone(errors, email, phone, "Phone");
        VendorValidation.ValidateWebsite(errors, website);
        VendorValidation.ValidateExtras(errors, alternateEmail, alternatePhone, documents);
        VendorValidation.ValidateRange(errors, "ContractValue", contractValue, 0);
        VendorValidation.ValidateRange(errors, "RenewalNoticeDays", renewalNoticeDays, 0, 365);
        VendorValidation.ValidateRange(errors, "VisitsPerYear", visitsPerYear, 0, 365);
        VendorValidation.ValidateRange(errors, "ResponseHours", responseHours, 1);
        VendorValidation.ValidateRange(errors, "ResolutionHours", resolutionHours, 1);
        VendorValidation.ValidateRange(errors, "UptimePercent", uptimePercent, 0, 100);
        VendorValidation.ValidateRange(errors, "PenaltyPerHour", penaltyPerHour, 0);

        if (responseHours is not null && resolutionHours is not null && resolutionHours < responseHours)
        {
            errors["ResolutionHours"] =
                ["ResolutionHours cannot be less than ResponseHours"];
        }

        return errors;
    }
}
