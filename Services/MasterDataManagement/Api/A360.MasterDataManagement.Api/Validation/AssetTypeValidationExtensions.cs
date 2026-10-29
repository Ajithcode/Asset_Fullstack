using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class AssetTypeValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateAssetTypeRequest request)
    {
        return ValidateFields(request.AssetTypeName, request.Code, request.Category, request.DefaultLifetime);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateAssetTypeRequest request)
    {
        return ValidateFields(request.AssetTypeName, request.Code, request.Category, request.DefaultLifetime);
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
}
