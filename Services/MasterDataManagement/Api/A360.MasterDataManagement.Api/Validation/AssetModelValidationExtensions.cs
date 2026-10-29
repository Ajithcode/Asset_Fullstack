using A360.MasterDataManagement.Api.Contracts;

namespace A360.MasterDataManagement.Api.Validation;

public static class AssetModelValidationExtensions
{
    public static Dictionary<string, string[]> Validate(
        this CreateAssetModelRequest request)
    {
        return ValidateFields(request.ModelName, request.ModelCode, request.Manufacturer, request.AssetType, request.Category);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateAssetModelRequest request)
    {
        return ValidateFields(request.ModelName, request.ModelCode, request.Manufacturer, request.AssetType, request.Category);
    }

    public static Dictionary<string, string[]> Validate(
        this UpdateAssetModelStatusRequest request)
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
        string? modelName,
        string? modelCode,
        MasterDataReferenceDto? manufacturer,
        MasterDataReferenceDto? assetType,
        MasterDataReferenceDto? category)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(modelName))
        {
            errors["ModelName"] =
                ["ModelName is required"];
        }

        if (string.IsNullOrWhiteSpace(modelCode))
        {
            errors["ModelCode"] =
                ["ModelCode is required"];
        }

        // Manufacturer, asset type and category are records in this service, so they are picked by id.
        if (manufacturer is not null && string.IsNullOrWhiteSpace(manufacturer.Id))
        {
            errors["Manufacturer"] =
                ["Manufacturer must have a manufacturer id"];
        }

        if (assetType is not null && string.IsNullOrWhiteSpace(assetType.Id))
        {
            errors["AssetType"] =
                ["AssetType must have an asset type id"];
        }

        if (category is not null && string.IsNullOrWhiteSpace(category.Id))
        {
            errors["Category"] =
                ["Category must have a category id"];
        }

        return errors;
    }
}
