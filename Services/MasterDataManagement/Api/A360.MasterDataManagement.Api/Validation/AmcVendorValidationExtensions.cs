using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class AmcVendorValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateAmcVendorRequest request)
    {
        return ValidateFields(
            request.AmcVendorName, request.Code, request.StartDate, request.EndDate, request.AssetType, request.Email);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateAmcVendorRequest request)
    {
        return ValidateFields(
            request.AmcVendorName, request.Code, request.StartDate, request.EndDate, request.AssetType, request.Email);
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
        string? email)
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

        // The asset type is a record in this service, so it is picked by id.
        if (assetType is not null && string.IsNullOrWhiteSpace(assetType.Id))
        {
            errors["AssetType"] =
                ["AssetType must have an asset type id"];
        }

        ContactValidation.ValidateEmailAndPhone(errors, email, null);

        return errors;
    }
}
