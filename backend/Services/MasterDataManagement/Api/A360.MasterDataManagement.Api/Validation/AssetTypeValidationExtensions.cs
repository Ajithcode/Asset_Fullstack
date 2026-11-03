using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class AssetTypeValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateAssetTypeRequest request)
    {
        var errors = ValidateFields(request.AssetTypeName, request.Code, request.Category, request.DefaultLifetime);
        ValidateDetails(errors, request.WarrantyMonths, request.UsefulLifeMonths, request.ResidualPercent, request.CapitalizationThreshold, request.CustomFields);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateAssetTypeRequest request)
    {
        var errors = ValidateFields(request.AssetTypeName, request.Code, request.Category, request.DefaultLifetime);
        ValidateDetails(errors, request.WarrantyMonths, request.UsefulLifeMonths, request.ResidualPercent, request.CapitalizationThreshold, request.CustomFields);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateAssetTypeStatusRequest request)
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
        string? assetTypeName,
        string? code,
        MasterDataReferenceDto? category,
        int? defaultLifetime)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(assetTypeName))
        {
            errors["AssetTypeName"] =
                ["AssetTypeName is required"];
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            errors["Code"] =
                ["Code is required"];
        }

        // The category is a record in this service, so it is picked by id.
        if (category is not null && string.IsNullOrWhiteSpace(category.Id))
        {
            errors["Category"] =
                ["Category must have a category id"];
        }

        if (defaultLifetime < 0)
        {
            errors["DefaultLifetime"] =
                ["DefaultLifetime cannot be negative"];
        }

        return errors;
    }

    private static void ValidateDetails(
        Dictionary<string, string[]> errors,
        int? warrantyMonths,
        int? usefulLifeMonths,
        decimal? residualPercent,
        decimal? capitalizationThreshold,
        List<AssetCustomFieldDto>? customFields)
    {
        if (warrantyMonths is < 0 or > 240)
        {
            errors["WarrantyMonths"] =
                ["WarrantyMonths must be between 0 and 240"];
        }

        if (usefulLifeMonths is < 1 or > 600)
        {
            errors["UsefulLifeMonths"] =
                ["UsefulLifeMonths must be between 1 and 600"];
        }

        if (residualPercent is < 0 or > 100)
        {
            errors["ResidualPercent"] =
                ["ResidualPercent must be between 0 and 100"];
        }

        if (capitalizationThreshold < 0)
        {
            errors["CapitalizationThreshold"] =
                ["CapitalizationThreshold cannot be negative"];
        }

        AssetCustomFieldValidation.ValidateList(errors, customFields);
    }
}
